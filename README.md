# LegendsTeamVN Badminton Club

[![CI/CD Pipeline](https://github.com/LegendsTeamVN/LegendsTeamVN.BadmintonClub/actions/workflows/deploy.yml/badge.svg)](https://github.com/LegendsTeamVN/LegendsTeamVN.BadmintonClub/actions/workflows/deploy.yml)

*[Read this in Vietnamese (Đọc bằng Tiếng Việt)](README.VI.md)*

A modern, robust Backend API for managing the LegendsTeamVN Badminton Club. The project is built on **.NET 10** using **Clean Architecture**, **CQRS**, and **Domain-Driven Design (DDD)** principles to ensure scalability, maintainability, and testability.

## 🚀 Tech Stack

- **Framework**: .NET 10, ASP.NET Core Minimal APIs
- **Architecture**: Clean Architecture, CQRS (MediatR)
- **Database**: SQL Server 2022 (Entity Framework Core)
- **Caching**: Redis
- **Message Broker**: RabbitMQ (MassTransit)
- **Infrastructure**: Docker, Nginx Proxy Manager
- **CI/CD**: GitHub Actions, GitHub Container Registry (GHCR)

## 📁 Project Structure

- `src/BuildingBlocks/`: Shared libraries, utilities, and core architectural components.
- `src/LegendsTeamVN.BadmintonClub.*`: The core domain, application logic, and persistence layers following Clean Architecture.
- `src/Hosts/LegendsTeamVN.BadmintonClub.API`: The main API entry point (Minimal APIs).
- `src/Hosts/LegendsTeamVN.BadmintonClub.Migrator`: A dedicated worker project to safely manage and apply Entity Framework migrations.
- `infrastructure/`: Contains `docker-compose.yml` for quickly setting up local dependencies (SQL Server, Redis, RabbitMQ, NPM).
- `tests/`: Architecture tests (NetArchTest) to enforce dependency rules.

## 🛠️ Setup & Installation

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker & Docker Compose](https://www.docker.com/)

### Step 1: Start Infrastructure (Local Dependencies)

Before running the API, you need to spin up the required infrastructure (Database, Cache, Message Broker).

1. Clone the repository:
   ```bash
   git clone https://github.com/LegendsTeamVN/LegendsTeamVN.BadmintonClub.git
   cd LegendsTeamVN.BadmintonClub
   ```

2. Start the local infrastructure:
   ```bash
   cd infrastructure
   docker compose up -d
   cd ..
   ```

### Step 2: Database Migrations

The project uses a dedicated `Migrator` project. You must generate and apply migrations before the first run.

**Option 1: Using Package Manager Console (Visual Studio)**
- Open PMC and set `src\Hosts\LegendsTeamVN.BadmintonClub.Migrator` as the Default Project.
```powershell
# Generate & Apply BadmintonDbContext
Add-Migration -Context BadmintonDbContext Init -OutputDir Migrations
Update-Database -Context BadmintonDbContext

# Generate & Apply AppIdentityDbContext
Add-Migration -Context AppIdentityDbContext InitIdentity -OutputDir Migrations
Update-Database -Context AppIdentityDbContext
```

**Option 2: Using .NET CLI (Terminal)**
- Run these commands from the root directory:
```bash
# Generate & Apply BadmintonDbContext
dotnet ef migrations add Init -c BadmintonDbContext -p src/Hosts/LegendsTeamVN.BadmintonClub.Migrator -s src/Hosts/LegendsTeamVN.BadmintonClub.Migrator -o Migrations
dotnet ef database update -c BadmintonDbContext -p src/Hosts/LegendsTeamVN.BadmintonClub.Migrator -s src/Hosts/LegendsTeamVN.BadmintonClub.Migrator

# Generate & Apply AppIdentityDbContext
dotnet ef migrations add InitIdentity -c AppIdentityDbContext -p src/Hosts/LegendsTeamVN.BadmintonClub.Migrator -s src/Hosts/LegendsTeamVN.BadmintonClub.Migrator -o Migrations
dotnet ef database update -c AppIdentityDbContext -p src/Hosts/LegendsTeamVN.BadmintonClub.Migrator -s src/Hosts/LegendsTeamVN.BadmintonClub.Migrator
```

### Step 3: Run the API

Once the database is ready, you can start the backend API:

**Run via .NET CLI:**
```bash
dotnet run --project src/Hosts/LegendsTeamVN.BadmintonClub.API
```

**Run via Docker Compose (Root directory):**
*Note: Make sure you create a `.env` file containing all required environment variables (DB_CONNECTION_STRING, REDIS_CONNECTION_STRING, JWT secrets, etc.) before running this.*
```bash
docker compose up -d
```

## 📚 API Documentation

Once the API is running, you can explore and test the endpoints using the built-in Swagger UI:
- **Local URL**: `http://localhost:8080/swagger` (or the port you configured)
- **Features**: Interactive API documentation, schema definitions, and full JWT Authentication support.

## 🏗️ Development Guidelines

To ensure the codebase remains clean and maintainable, we have documented the core Architecture, Database Design, API Guidelines, and Multi-Group RBAC in one comprehensive guide:

👉 **[Comprehensive Development Guide](docs/DEVELOPMENT_GUIDE.md)**

## 📜 License

This project is licensed under the GPL-3.0 License - see the [LICENSE](LICENSE.txt) for details.
