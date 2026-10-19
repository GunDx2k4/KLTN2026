---
trigger: model_decision
description: "Apply Conventional Commits (v1.0.0) guidelines when generating commit messages, reviewing git diffs, staging changes, or executing git commit commands."
---

# Conventional Commits Standard (v1.0.0) & Quick Reference

Format all Git commit messages strictly according to the Conventional Commits v1.0.0 specification with natural, accented Vietnamese descriptions:

```text
<type>(<scope>): <tiêu đề ngắn gọn mô tả thay đổi bằng tiếng Việt>

[optional body - giải thích tại sao và thay đổi cái gì]

[optional footer(s) - ghi chú BREAKING CHANGE hoặc link issue]
```

---

## 1. Quick Registry
- **Allowed Types:** `feat`, `fix`, `refactor`, `perf`, `style`, `test`, `docs`, `build`, `ci`, `chore`, `revert`.
- **Project Scopes:** `club`, `session`, `finance`, `auth`, `notification`, `db`, `arch`, `deps`, `rules`, `skills`.
- **Header Rules:** Lowercase start after `: `, no trailing period, total header length <= 72 characters.
- **Vietnamese Language:** Prefer clear, natural, accented Vietnamese in UTF-8 (e.g. `feat(club): thêm api duyệt yêu cầu gia nhập`).
- **Zero Emoji Policy:** Strictly forbidden to include emojis or icons anywhere in commit messages.

---

## 2. Skill Navigation
For multi-file changes, staging splits, or commit execution, invoke the `conventional-commits` skill and its validation script:

```powershell
python .agents/skills/conventional-commits/scripts/git_commit_helper.py --validate "<commit_message>"
```

---

## 3. Commit Safety Guardrail
Never execute `git commit` automatically without first presenting the proposed commit message and affected files for user confirmation.
