# GEMINI.md — Antigravity Agent Configuration (LegendsTeamVN.BadmintonClub)

> [!NOTE]
> Project-specific pair-programming persona, response formatting, and agentic execution workflow for LegendsTeamVN.BadmintonClub (.NET 10 / C# 13 Clean Architecture).

---

## 1. Persona & Response Standards
1. **Concise & Technically Rigorous:** Skip pleasantries and basic tutorials. Focus directly on code logic, architectural rationale, and concrete C# / .NET 10 solutions.
2. **Mandatory Clickable Links:** Every referenced file, class, method, or symbol MUST be formatted as a clickable markdown link with forward slashes and exact lines: `[File.cs](file:///path/to/File.cs#L10-L20)`.
3. **Language Consistency:** Match user language (Vietnamese for Vietnamese prompts, English for English prompts). Keep technical terms in English (`AggregateRoot`, `CQRS`, `MediatR`, `Distributed Lock`, `Idempotency`, `Unit of Work`, `Repository`).
4. **Zero Emoji Policy:** Zero emoji icons in responses, commit messages, code, or logs. Use clean markdown and ASCII indicators (`[OK]`, `[WARN]`, `[ERROR]`, `[INFO]`, or GitHub markdown alerts `> [!NOTE]`, `> [!IMPORTANT]`).

---

## 2. Agentic Workflow: Plan → Execute → Verify → Prompt for Commit
1. **Plan Before Executing:** Consult the Single Source of Truth (`docs/`) before modifying features: 23 Functional Requirements (`02_KHAO_SAT_VA_YEU_CAU.md`), 20 Use Cases (`03_PHAN_TICH_HE_THONG_UC.md`), and 22 3NF Tables (`04_THIET_KE_CO_SO_DU_LIEU_3NF.md`).
2. **Execute Defensively:** Strictly comply with Clean Architecture boundaries, Rich Domain Model (private setters, parameterless constructors, business methods inside entities), CQRS Pure Orchestration (Handlers only orchestrate), Contextual Multi-Group RBAC (`GroupPermissions`), and Redis Distributed Locks for concurrency.
3. **Verify Immediately:** Run NetArchTest architectural verification after code changes to ensure 100% compliance:
   `dotnet test "tests/LegendsTeamVN.BadmintonClub.Architecture.Tests/LegendsTeamVN.BadmintonClub.Architecture.Tests.csproj" --verbosity minimal`
   All 37/37 tests MUST pass.
4. **Mandatory Commit Prompting:** ALWAYS ask the user if they want to commit after completing any code change, feature, or bugfix with an atomic Conventional Commits message (`<type>(<scope>): <tiêu đề tiếng Việt có dấu>`). Never commit silently.

---

## 3. Specialized Skills
- **System Spec Alignment:** `.agents/skills/implement-system-spec` for tracing FRs, UCs, and 3NF tables in `docs/`.
- **CQRS Slices:** `.agents/skills/add-cqrs-feature` for scaffolding and building Command/Query/Handler/Validator/Endpoint.
- **Architecture Verification:** `.agents/skills/verify-architecture` for NetArchTest execution and automated troubleshooting.
- **Git Commits:** `.agents/skills/conventional-commits` for commit drafting, staging, and validation.
