---
name: conventional-commits
description: >-
  Generates, validates, and stages Git commits strictly following Conventional Commits v1.0.0 and LegendsTeamVN subsystem scopes in natural Vietnamese. Use when committing code, staging files, drafting commit messages, organizing diffs, or when asked to 'commit', 'git commit', 'tạo commit', 'viết commit', 'lưu code', 'stage changes'.
---

# Conventional Commits Workflow & Atomicity Enforcer

## Goal (Single Responsibility)
Analyze working tree changes, enforce single-responsibility commit atomicity, select appropriate LegendsTeamVN subsystem scopes, and generate or execute standardized Conventional Commits (v1.0.0) messages in Vietnamese.

---

## Decision Tree: Commit Workflow

```text
User requests Git commit or commit message drafting
  │
  ├── 1. Kiểm tra trạng thái git:
  │      ├── Nếu KHÔNG CÓ THAY ĐỔI (working tree clean): Thông báo cho người dùng và DỪNG LẠI.
  │      └── Nếu CÓ THAY ĐỔI: Chuyển sang Bước 2.
  │
  ├── 2. Đánh giá tính nguyên tử (Atomicity Check via script):
  │      ├── Nếu [OK] Changes appear atomic: Tiến hành tạo 1 commit duy nhất.
  │      └── Nếu [WARN] Multi-subsystem split recommended: Lập kế hoạch chia nhỏ commit (đọc ./examples/commit_examples.md).
  │
  ├── 3. Xác định cấu trúc Header <type>(<scope>): <subject>:
  │      ├── <type>: feat, fix, refactor, perf, style, test, docs, build, ci, chore, revert.
  │      ├── <scope>: club, session, finance, auth, notification, db, arch, deps, rules, skills.
  │      └── <subject>: Chữ thường tiếng Việt có dấu, không dấu chấm cuối, tổng header <= 72 ký tự.
  │
  └── 4. Thực thi hoặc Đề xuất:
         ├── Nếu người dùng bảo soạn thảo (draft): Xuất khối commit message và lệnh mẫu.
         └── Nếu người dùng bảo commit trực tiếp: Stage file (`git add`), thực thi `git commit`, và kiểm tra lại bằng `git log -n 1 --stat`.
```

---

## Black-Box Script Invocations

> [!IMPORTANT]
> **Use Scripts as Black Boxes:** Do NOT read or load the Python script source code into context. Run the CLI directly or pass `--help` to inspect arguments.

1. **Phân tích Diff & Kiểm tra Tính Nguyên tử:**
   ```powershell
   python .agents/skills/conventional-commits/scripts/git_commit_helper.py
   ```
   *(Thêm cờ `--json` để nhận dữ liệu có cấu trúc nếu cần).*

2. **Kiểm tra Tính Hợp lệ của Commit Message Đề xuất:**
   ```powershell
   python .agents/skills/conventional-commits/scripts/git_commit_helper.py --validate "<proposed_commit_message>"
   ```
   *(Bắt buộc kiểm tra exit code `0` trước khi kết luận).*

---

## Step-by-Step Procedure

1. **Chạy Phân tích:** Thực thi `git_commit_helper.py` để phát hiện các file sửa đổi và nhóm subsystem scopes.
2. **Xử lý Phân nhánh:** Áp dụng Decision Tree. Nếu thay đổi trải rộng nhiều scope không liên quan, lập danh sách staged commits độc lập theo mẫu trong [`examples/commit_examples.md`](examples/commit_examples.md).
3. **Soạn thảo Header & Body:** Đối chiếu danh mục scope tại [`references/conventional_spec.md`](references/conventional_spec.md). Tiêu đề viết bằng tiếng Việt tự nhiên, có dấu.
4. **Xác thực Cú pháp:** Chạy lệnh `--validate` với message vừa soạn. Đảm bảo đạt `[OK]`.
5. **Xác nhận với Người Dùng:** Luôn trình bày thông điệp commit và danh sách file thay đổi trước khi chạy lệnh commit thực tế.

---

## Constraints & Guardrails
- **Header <= 72 chars:** Toàn bộ dòng tiêu đề không bao giờ vượt quá 72 ký tự.
- **Zero Emoji Policy:** Tuyệt đối không dùng emoji trong commit message.
- **Tiếng Việt có dấu:** Sử dụng tiếng Việt chuẩn UTF-8, mô tả chính xác nội dung thay đổi.
- **Không dùng từ mơ hồ:** Cấm dùng `update code`, `fix bugs`, `wip`, `misc`.
