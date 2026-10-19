# Conventional Commits Specification & Scope Registry (LegendsTeamVN)

## 1. Allowed Types
- `feat`: A new user-facing feature or use case.
- `fix`: A bug fix in domain, application, or presentation logic.
- `refactor`: Code restructuring without modifying external behavior.
- `perf`: Performance enhancement (e.g. Redis caching, SQL optimization).
- `style`: Whitespace, code formatting, removing unused usings.
- `test`: Adding or correcting tests (Unit tests, NetArchTest architecture tests).
- `docs`: Documentation updates, README, or API docs.
- `build`: Build system or project configuration (`.csproj`, `.slnx`).
- `ci`: CI/CD workflows, Dockerfiles, docker-compose.
- `chore`: Housekeeping, `.gitignore`, repo configurations.
- `revert`: Reverting a previous commit.

## 2. Project Scopes
- `club`: Club profiles, schedules, members, custom roles, join requests.
- `session`: Badminton sessions, check-in, court rotation, match management.
- `finance`: Ledger entries, fee calculation, VietQR, SePay webhook, bank config.
- `auth`: User registration, login, JWT claims, password hashing, FCM devices.
- `notification`: Firebase Cloud Messaging (FCM) notifications.
- `db`: EF Core migrations, DbContext configurations, entity mapping.
- `arch`: Clean Architecture boundaries, NetArchTest rules.
- `deps`: NuGet package updates, Directory.Packages.props.
- `rules`: Antigravity rules, AGENTS.md, GEMINI.md.
- `skills`: Agent skill definitions, scripts, and workflows.

## 3. Formatting Invariants
- Total header `<= 72` characters.
- Lowercase start immediately following `<type>(<scope>): `.
- No trailing period (`.`).
- Zero Emoji Policy.
- Clear, natural, accented Vietnamese in UTF-8.
