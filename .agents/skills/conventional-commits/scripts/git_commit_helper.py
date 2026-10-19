import os
import sys
import re
import subprocess
import argparse
import json

# Force UTF-8 output on Windows consoles
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

VALID_TYPES = {
    "feat": "Thêm một tính năng mới hoặc một use case mới",
    "fix": "Sửa lỗi trong logic hoặc giao diện API",
    "refactor": "Tái cấu trúc mã nguồn mà không đổi hành vi",
    "perf": "Cải thiện hiệu năng xử lý hoặc tối ưu truy vấn",
    "style": "Chỉnh sửa định dạng code, xóa using thừa",
    "test": "Thêm mới hoặc chỉnh sửa các bài kiểm thử",
    "docs": "Thêm hoặc cập nhật tài liệu",
    "build": "Thay đổi cấu hình build, project files (.csproj, .slnx)",
    "ci": "Thay đổi file cấu hình CI/CD (GitHub Actions, Dockerfile)",
    "chore": "Bảo trì, cập nhật cấu hình công cụ (.gitignore)",
    "revert": "Hoàn tác một commit trước đó"
}

VALID_SCOPES = {
    "club", "session", "finance", "auth", "notification", "db", "arch", "deps",
    "rules", "skills", "ci", "build", "chore"
}

SCOPE_MAPPINGS = [
    (r"src/LegendsTeamVN\.BadmintonClub\.Application/Features/Clubs/", "club"),
    (r"src/LegendsTeamVN\.BadmintonClub\.Application/Features/Sessions/", "session"),
    (r"src/LegendsTeamVN\.BadmintonClub\.Application/Features/Finance/", "finance"),
    (r"src/LegendsTeamVN\.BadmintonClub\.Application/Features/Auth/", "auth"),
    (r"src/LegendsTeamVN\.BadmintonClub\.Application/Features/Notifications/", "notification"),
    (r"src/LegendsTeamVN\.BadmintonClub\.Domain/Entities/(Group|Club|Member|Role)", "club"),
    (r"src/LegendsTeamVN\.BadmintonClub\.Domain/Entities/(Session|Booking|Match)", "session"),
    (r"src/LegendsTeamVN\.BadmintonClub\.Domain/Entities/(Fee|Ledger|Bank)", "finance"),
    (r"src/LegendsTeamVN\.BadmintonClub\.Presentation/Endpoints/(Club|Group|Member)", "club"),
    (r"src/LegendsTeamVN\.BadmintonClub\.Presentation/Endpoints/Session", "session"),
    (r"src/LegendsTeamVN\.BadmintonClub\.Presentation/Endpoints/Finance", "finance"),
    (r"src/LegendsTeamVN\.BadmintonClub\.Presentation/Endpoints/Auth", "auth"),
    (r"src/LegendsTeamVN\.BadmintonClub\.Persistence/Migrations/", "db"),
    (r"src/LegendsTeamVN\.BadmintonClub\.Persistence/", "db"),
    (r"tests/.*Architecture\.Tests/", "arch"),
    (r"\.agents/skills/", "skills"),
    (r"\.agents/rules/", "rules"),
    (r"\.agents/", "rules"),
    (r"(AGENTS|GEMINI)\.md$", "rules"),
    (r"\.github/", "ci"),
    (r"\.(slnx|csproj)$", "build"),
    (r"Directory\.(Build|Packages)\.props$", "deps"),
    (r"\.(gitignore|gitattributes)$", "chore"),
    (r"\.editorconfig$", "chore")
]

def find_repo_root():
    current = os.path.abspath(os.path.dirname(__file__))
    while current and current != os.path.dirname(current):
        if os.path.exists(os.path.join(current, ".git")):
            return current
        current = os.path.dirname(current)
    return os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))

def run_git(args, cwd=None):
    if cwd is None:
        cwd = find_repo_root()
    cmd = ["git", "-C", cwd] + args
    try:
        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding="utf-8", errors="replace")
        return proc.returncode, proc.stdout.rstrip(), proc.stderr.strip()
    except Exception as e:
        return 1, "", str(e)

def validate_commit_message(msg):
    msg = msg.strip()
    if not msg:
        return False, "[ERROR] Commit message is empty."

    lines = msg.splitlines()
    header = lines[0].strip()

    pattern = r"^([a-z]+)(\([a-z0-9_-]+\))?(!)?:\s+(.+)$"
    match = re.match(pattern, header)
    if not match:
        return False, f"[ERROR] Header does not match '<type>(<scope>): <subject>'. Got: '{header}'"

    commit_type, raw_scope, breaking_excl, subject = match.groups()

    if commit_type not in VALID_TYPES:
        return False, f"[ERROR] Unknown type '{commit_type}'. Allowed: {', '.join(sorted(VALID_TYPES.keys()))}"

    if raw_scope:
        scope = raw_scope.strip("()")
        if scope not in VALID_SCOPES:
            return False, f"[ERROR] Unknown scope '{scope}'. Allowed: {', '.join(sorted(VALID_SCOPES))}"

    if len(header) > 72:
        return False, f"[ERROR] Header exceeds 72 characters ({len(header)} chars): '{header}'"

    if subject.endswith("."):
        return False, "[ERROR] Subject must not end with a period ('.')."

    # Check zero emoji policy
    emoji_pattern = re.compile("[\U00010000-\U0010ffff\u2600-\u26ff\u2700-\u27bf]", flags=re.UNICODE)
    if emoji_pattern.search(msg):
        return False, "[ERROR] Zero Emoji Policy: Emojis are strictly forbidden in commit messages."

    return True, "[OK] Commit message conforms to Conventional Commits specification."

def map_file_to_scope(filepath):
    normalized = filepath.replace("\\", "/")
    for pattern, scope in SCOPE_MAPPINGS:
        if re.search(pattern, normalized):
            return scope
    return "chore"

def analyze_git_status():
    repo_root = find_repo_root()
    rc, stdout, stderr = run_git(["status", "--porcelain"], cwd=repo_root)
    if rc != 0:
        return {"error": f"Failed to execute git status: {stderr}"}

    lines = [line for line in stdout.splitlines() if line.strip()]
    if not lines:
        return {"status": "clean", "files": [], "scopes": [], "is_atomic": True}

    staged_files = []
    unstaged_files = []
    scopes = set()

    for line in lines:
        status_code = line[:2]
        path = line[3:].strip()
        if " -> " in path:
            path = path.split(" -> ")[1].strip()

        scope = map_file_to_scope(path)
        scopes.add(scope)

        item = {"path": path, "status": status_code, "scope": scope}
        if status_code[0] in "MADRC":
            staged_files.append(item)
        if status_code[1] in "MADU?":
            unstaged_files.append(item)

    is_atomic = len(scopes) <= 2

    return {
        "status": "dirty",
        "staged": staged_files,
        "unstaged": unstaged_files,
        "scopes": sorted(list(scopes)),
        "is_atomic": is_atomic
    }

def main():
    parser = argparse.ArgumentParser(description="Conventional Commits Helper for LegendsTeamVN.BadmintonClub")
    parser.add_argument("--validate", type=str, help="Validate a proposed commit message string")
    parser.add_argument("--json", action="store_true", help="Output status analysis in JSON format")
    args = parser.parse_args()

    if args.validate:
        valid, message = validate_commit_message(args.validate)
        print(message)
        sys.exit(0 if valid else 1)

    analysis = analyze_git_status()
    if args.json:
        print(json.dumps(analysis, indent=2, ensure_ascii=False))
        return

    if "error" in analysis:
        print(f"[ERROR] {analysis['error']}")
        sys.exit(1)

    if analysis["status"] == "clean":
        print("[OK] Working tree clean. Nothing to commit.")
        return

    print("=== LegendsTeamVN Git Working Tree Analysis ===")
    print(f"Detected Scopes: {', '.join(analysis['scopes'])}")
    if analysis["is_atomic"]:
        print("[OK] Changes appear atomic (<= 2 distinct scopes).")
    else:
        print(f"[WARN] Multi-subsystem split recommended: Changes span {len(analysis['scopes'])} scopes ({', '.join(analysis['scopes'])}). Consider splitting into multiple commits.")

    if analysis["staged"]:
        print("\nStaged files:")
        for f in analysis["staged"]:
            print(f"  [{f['status']}] {f['path']} ({f['scope']})")

    if analysis["unstaged"]:
        print("\nUnstaged / Untracked files:")
        for f in analysis["unstaged"]:
            print(f"  [{f['status']}] {f['path']} ({f['scope']})")

if __name__ == "__main__":
    main()
