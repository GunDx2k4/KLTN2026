# AGENTS.md — LegendsTeamVN.BadmintonClub Universal Standards

Universal architectural standards, invariants, and safety guardrails across all AI pair-programming tools.

---

## 1. Tech Stack & Target Environment
- **Platform:** .NET 10 (C# 13/14).
- **Architecture:** Clean Architecture 4 Tầng + CQRS (MediatR) + Vertical Slices.
- **Database:** PostgreSQL 16+ via EF Core 10 & Npgsql.
- **Distributed Caching & Locks:** Redis (StackExchange.Redis).
- **Validation:** FluentValidation 11+.
- **Architecture Testing:** NetArchTest.Rules + xUnit + FluentAssertions (37/37 automated checks).
- **Realtime:** ASP.NET Core SignalR.
- **External Integrations:** VietQR (NAPAS 247), SePay Webhook, Firebase Cloud Messaging (FCM).

---

## 2. Core Architectural Invariants
1. **Clean Architecture Dependency Direction:** `Presentation -> Application -> Domain <- Persistence`, with `Infrastructure` implementing application abstractions. `Domain` has zero external dependencies.
2. **Rich Domain Model:** All state transitions, business rules, and cost calculations are encapsulated inside Domain Entities (`session.CheckIn()`, `session.CalculateTotalCost()`). Public setters are forbidden; all properties must have `private set;` or `protected set;`. Parameterless constructors (`protected`) are mandatory for ORM reflection. Domain events must be `sealed class` with `DomainEvent` postfix.
3. **CQRS Pure Orchestration:** Handlers only coordinate (Load entity via Repository -> Check existence -> Call entity business method -> Save changes -> Return `Result<T>`). No business calculation, conditional club rules, or raw SQL in Handlers. Handlers and Validators must be `sealed`.
4. **Minimal API Endpoints:** Endpoints inherit `EndpointGroupBase`, end with `Endpoint` postfix, and only inject `ISender`. Responses must be formatted using `result.Match(onSuccess: data => Results.Ok(data))`.
5. **Contextual Multi-Group Dynamic RBAC:** Club-scoped routes (`{id:guid}`, `{groupId:guid}`, `{clubId:guid}`) must be protected by `.RequireGroupPermission(GroupPermissions.*)`. Hardcoding permission strings is forbidden. Global query filters locking `tenant_id` are forbidden (users participate across multiple clubs with independent roles).
6. **Financial Safety & Concurrency:** All currency amounts must use `decimal` (never `float` or `double`). Redis Distributed Locks are mandatory for `BookSlot`, settlement, and SePay webhooks. Webhooks must be idempotent. Ledger entries and session fee status changes must execute inside a single Database Transaction (Unit of Work).
7. **Zero Emoji Policy:** Never use emojis in source code, commits, logs, or chat responses. Use standard ASCII markers (`[OK]`, `[WARN]`, `[ERROR]`, `[INFO]`) or markdown alerts.
8. **Mandatory Architecture Verification:** Before concluding any task, verify that all 37/37 NetArchTest tests pass:
   `dotnet test "tests/LegendsTeamVN.BadmintonClub.Architecture.Tests/LegendsTeamVN.BadmintonClub.Architecture.Tests.csproj" --verbosity minimal`
9. **Mandatory Commit Prompting:** Always ask the user if they want to commit after completing any feature addition, bug fix, refactor, or code change with an atomic Conventional Commits message (`<type>(<scope>): <tiêu đề tiếng Việt có dấu>`). Never commit silently.

---

## 3. Modular Rules Reference
Detailed domain standards are modularized in `.agents/rules/`:
- **Clean Architecture Boundaries:** `.agents/rules/clean-architecture-boundaries.md` (Trigger: `src/**, tests/**`)
- **System Spec Alignment (SSOT):** `.agents/rules/system-spec-and-design-alignment.md` (Trigger: `model_decision`)
- **Dynamic Multi-Group RBAC:** `.agents/rules/tenant-rbac-and-security.md` (Trigger: `src/LegendsTeamVN.BadmintonClub.Presentation/**, src/LegendsTeamVN.BadmintonClub.Application/**`)
- **Financial & Concurrency:** `.agents/rules/financial-and-concurrency.md` (Trigger: `src/**/Finance/**, src/**/Session**, src/**/Entities/**Fee**, src/**/Entities/**Booking**`)
- **Git & Commits:** `.agents/rules/conventional-commits.md` (Trigger: `model_decision`)
