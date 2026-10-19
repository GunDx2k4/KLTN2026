---
trigger: model_decision
description: "Quy ước tạo git commit message chuẩn Conventional Commits với mô tả Tiếng Việt có dấu đầy đủ, chuẩn UTF-8."
---

# Quy Uoc Commit Code Chuan (Conventional Commits v1.0.0)

> [!NOTE]
> Quy chuẩn tạo commit code thống nhất cho toàn bộ kỹ sư và AI Agent trên dự án **LegendsTeamVN.BadmintonClub**.

Format chuẩn:
```text
<type>(<scope>): <tiêu đề ngắn gọn mô tả thay đổi bằng tiếng Việt>

[optional body - giải thích tại sao và thay đổi cái gì]

[optional footer(s) - ghi chú BREAKING CHANGE hoặc link issue]
```

---

## 1. Quick Registry
- **Allowed Types:** `feat`, `fix`, `refactor`, `perf`, `style`, `test`, `docs`, `build`, `ci`, `chore`, `revert`.
- **Project Scopes:** `club`, `session`, `finance`, `auth`, `notification`, `db`, `arch`, `deps`, `rules`, `skills`.
- **Header Rules:** Chữ thường sau `: `, không dấu chấm cuối câu, tổng header <= 72 ký tự.
- **Tiêu đề Tiếng Việt:** Sử dụng Tiếng Việt có dấu chuẩn UTF-8 (ví dụ: `feat(club): thêm api duyệt yêu cầu gia nhập`).
- **Zero Emoji Policy:** Tuyệt đối không dùng emoji trong commit message.

---

## 2. Skill Navigation
Sử dụng skill `conventional-commits` và script kiểm tra:

```powershell
python .agents/skills/conventional-commits/scripts/git_commit_helper.py --validate "<commit_message>"
```

---

## 3. Commit Safety Guardrail
Luôn trình bày commit message và danh sách file thay đổi để người dùng xác nhận trước khi thực hiện commit.
