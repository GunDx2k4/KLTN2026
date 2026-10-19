import os
import sys
import subprocess
import re

# Force UTF-8 output on Windows consoles
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

TROUBLESHOOTING_GUIDE = {
    "Entities_ShouldHave_PrivateSetters": (
        "Entity có thuộc tính dùng public setter ('{ get; set; }').",
        "Đổi thành '{ get; private set; }' hoặc '{ get; protected set; }'. Viết method nghiệp vụ trong Entity để thay đổi giá trị."
    ),
    "Entities_ShouldHave_ParameterlessConstructor": (
        "Entity thiếu constructor không tham số cho ORM reflection.",
        "Thêm constructor protected: 'protected EntityName() { }'."
    ),
    "DomainEvents_Should_BeSealed": (
        "Domain Event class chưa có từ khóa 'sealed'.",
        "Khai báo: 'public sealed class ...DomainEvent : IDomainEvent'."
    ),
    "DomainEvents_ShouldHave_DomainEventPostfix": (
        "Domain Event class thiếu hậu tố 'DomainEvent'.",
        "Đổi tên class kết thúc bằng 'DomainEvent'."
    ),
    "CommandHandlers_ShouldBe_Sealed": (
        "CommandHandler class chưa có từ khóa 'sealed'.",
        "Thêm từ khóa 'sealed': 'public sealed class ...CommandHandler'."
    ),
    "CommandHandlers_ShouldHave_CommandHandlerPostfix": (
        "CommandHandler class thiếu hậu tố 'CommandHandler'.",
        "Đổi tên class kết thúc bằng 'CommandHandler'."
    ),
    "QueryHandlers_ShouldBe_Sealed": (
        "QueryHandler class chưa có từ khóa 'sealed'.",
        "Thêm từ khóa 'sealed': 'public sealed class ...QueryHandler'."
    ),
    "QueryHandlers_ShouldHave_QueryHandlerPostfix": (
        "QueryHandler class thiếu hậu tố 'QueryHandler'.",
        "Đổi tên class kết thúc bằng 'QueryHandler'."
    ),
    "Validators_ShouldBe_Sealed": (
        "Validator class chưa có từ khóa 'sealed'.",
        "Thêm từ khóa 'sealed': 'public sealed class ...Validator'."
    ),
    "Validators_ShouldHave_ValidatorPostfix": (
        "Validator class thiếu hậu tố 'Validator'.",
        "Đổi tên class kết thúc bằng 'Validator'."
    ),
    "Endpoints_ShouldHave_EndpointPostfix": (
        "Endpoint class thiếu hậu tố 'Endpoint'.",
        "Đổi tên class kết thúc bằng 'Endpoint' (ví dụ: 'ClubEndpoint')."
    ),
    "PresentationLayer_ShouldNotHaveDependencyOn_PersistenceLayer": (
        "Tầng Presentation phụ thuộc trực tiếp vào Persistence (inject DbContext hoặc Repository).",
        "Xóa DbContext/Repository injection trong Endpoint. Chỉ inject ISender."
    ),
    "ApplicationLayer_ShouldNotHaveDependencyOn_PersistenceLayer": (
        "Tầng Application có 'using Persistence...' hoặc gọi DbContext trực tiếp.",
        "Xóa using Persistence. Mọi truy cập dữ liệu phải qua Repository Interface."
    )
}

def find_test_project_path():
    base_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
    csproj = os.path.join(base_dir, "tests", "LegendsTeamVN.BadmintonClub.Architecture.Tests", "LegendsTeamVN.BadmintonClub.Architecture.Tests.csproj")
    return os.path.abspath(csproj)

def run_tests():
    csproj_path = find_test_project_path()
    if not os.path.exists(csproj_path):
        print(f"[ERROR] Could not find test project at: {csproj_path}")
        return False, []

    cmd = ["dotnet", "test", csproj_path, "--verbosity", "minimal"]
    try:
        proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding="utf-8", errors="replace")
        output = proc.stdout + "\n" + proc.stderr

        match_passed = re.search(r"Passed!\s+-\s+Failed:\s+(\d+),\s+Passed:\s+(\d+),\s+Skipped:\s+(\d+),\s+Total:\s+(\d+)", output)
        if match_passed:
            failed = int(match_passed.group(1))
            passed = int(match_passed.group(2))
            total = int(match_passed.group(4))

            if failed == 0 and passed == total:
                print(f"[OK] Architecture Tests Passed: {passed}/{total} (100%)")
                return True, []
            else:
                print(f"[ERROR] Architecture Tests Failed: {failed} failed, {passed} passed, Total: {total}")
        else:
            if proc.returncode != 0:
                print("[ERROR] Test execution failed or compilation error occurred.")
            else:
                print("[OK] Tests completed.")

        failed_tests = []
        for line in output.splitlines():
            line_strip = line.strip()
            if "Failed" in line_strip and "[" in line_strip:
                failed_tests.append(line_strip)
            for rule_name in TROUBLESHOOTING_GUIDE.keys():
                if rule_name in line_strip and "Failed" in line_strip:
                    if rule_name not in failed_tests:
                        failed_tests.append(rule_name)

        return (proc.returncode == 0), failed_tests
    except Exception as e:
        print(f"[ERROR] Exception running dotnet test: {e}")
        return False, []

def main():
    print("=== Running NetArchTest Architecture Verification ===")
    success, failures = run_tests()

    if not success and failures:
        print("\n--- Diagnostic & Remediation Guide ---")
        for fail in failures:
            matched = False
            for rule_name, (cause, fix) in TROUBLESHOOTING_GUIDE.items():
                if rule_name in fail:
                    print(f"\n[FAIL] {rule_name}:")
                    print(f"  Nguyên nhân: {cause}")
                    print(f"  Cách khắc phục: {fix}")
                    matched = True
                    break
            if not matched:
                print(f"\n[FAIL] {fail}")

    sys.exit(0 if success else 1)

if __name__ == "__main__":
    main()
