# Câu lạc bộ Cầu lông LegendsTeamVN

[![CI/CD Pipeline](https://github.com/LegendsTeamVN/LegendsTeamVN.BadmintonClub/actions/workflows/deploy.yml/badge.svg)](https://github.com/LegendsTeamVN/LegendsTeamVN.BadmintonClub/actions/workflows/deploy.yml)

*[Read this in English](README.md)*

Hệ thống Backend API hiện đại và mạnh mẽ để quản lý Câu lạc bộ Cầu lông LegendsTeamVN. Dự án được xây dựng trên nền tảng **.NET 10**, ứng dụng triệt để **Clean Architecture**, **CQRS** và các nguyên lý **Domain-Driven Design (DDD)** nhằm đảm bảo khả năng mở rộng, bảo trì và dễ dàng test.

## 🚀 Công nghệ sử dụng

- **Framework**: .NET 10, ASP.NET Core Minimal APIs
- **Kiến trúc**: Clean Architecture, CQRS (MediatR)
- **Cơ sở dữ liệu**: PostgreSQL / SQL Server (Entity Framework Core)
- **Caching**: Redis
- **Message Broker**: RabbitMQ (MassTransit)
- **Hạ tầng (Infrastructure)**: Docker, Nginx Proxy Manager
- **CI/CD**: GitHub Actions, GitHub Container Registry (GHCR)

## 📁 Cấu trúc Dự án

- `src/BuildingBlocks/`: Chứa các thư viện dùng chung, Utilities và cấu trúc cốt lõi của toàn hệ thống.
- `src/LegendsTeamVN.BadmintonClub.*`: Các tầng Domain, Application, và Persistence tuân thủ chặt chẽ Clean Architecture.
- `src/Hosts/LegendsTeamVN.BadmintonClub.API`: Project chính để chạy API (sử dụng Minimal APIs).
- `src/Hosts/LegendsTeamVN.BadmintonClub.Migrator`: Project Worker độc lập chuyên dùng để quản lý và chạy Entity Framework Migrations cực kỳ an toàn.
- `infrastructure/`: Thư mục chứa `docker-compose.yml` để dựng nhanh các dịch vụ hạ tầng ở máy local (PostgreSQL/SQL Server, Redis, RabbitMQ, NPM).
- `tests/`: Chứa các bài Unit Test kiến trúc (NetArchTest) để ngăn chặn việc gọi sai tầng Dependency.

## 🛠️ Hướng dẫn Cài đặt & Chạy dự án

### Yêu cầu hệ thống
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker & Docker Compose](https://www.docker.com/)

### Bước 1: Khởi động Hạ tầng (Infrastructure)

Trước khi chạy API, bạn cần khởi động Database, Cache và Message Broker.

1. Clone source code về máy:
   ```bash
   git clone https://github.com/LegendsTeamVN/LegendsTeamVN.BadmintonClub.git
   cd LegendsTeamVN.BadmintonClub
   ```

2. Bật hạ tầng thông qua Docker Compose:
   ```bash
   cd infrastructure
   docker compose up -d
   cd ..
   ```

### Bước 2: Chạy Database Migrations & Seeding

Dự án này sử dụng project độc lập có tên là `Migrator` để quản lý migrations và tự động nạp Seeder ban đầu (`IdentityDataSeeder`).

```bash
dotnet run --project src/Hosts/LegendsTeamVN.BadmintonClub.Migrator
```

*Tài khoản Admin mặc định sau khi nạp seeder:*
- **Username / Email**: `admin` (hoặc `admin@admin.com`)
- **Password**: `admin`

### Bước 3: Khởi chạy API

Sau khi Database đã có đầy đủ Schema, bạn có thể chạy API:

```bash
dotnet run --project src/Hosts/LegendsTeamVN.BadmintonClub.API
```

---

## 🔐 Phân Quyền & Hướng Dẫn Bật/Tắt Chế Độ Dev

Hệ thống phân quyền được xây dựng theo mô hình **RBAC (Role-Based Access Control)**:
- **`AppUsers`**: Quản lý tài khoản.
- **`AppRoles`**: Quản lý các vai trò (`Admin`, `Manager`, `User`).
- **`AppPermissions`**: Quản lý toàn bộ danh mục quyền hệ thống (`Name`, `DisplayName`, `GroupName`).
- **`AppRolePermissions`**: Bảng liên kết gán quyền cho từng Role với Khóa chính kết hợp `(RoleId, PermissionId)` và Foreign Keys ngăn ngừa trùng lặp.

### Hướng dẫn Bật/Tắt tính năng Kiểm tra (Validate) khi lên Production:

#### 1. Bật/Tắt Kiểm tra hết hạn Token (JWT Lifetime Validation)
📍 **[ServiceCollectionExtensions.cs](file:///c:/code/abp_be/LegendsTeamVN.BadmintonClub/src/BuildingBlocks/LegendsTeamVN.Core.Identity/DependencyInjection/Extensions/ServiceCollectionExtensions.cs#L65-L75)**

```csharp
options.TokenValidationParameters = new TokenValidationParameters
{
    ValidateIssuer = true,        // Set true khi lên Production
    ValidateAudience = true,      // Set true khi lên Production
    ValidateLifetime = true,      // Set true khi muốn bắt buộc kiểm tra Hạn sử dụng Token
    ValidateIssuerSigningKey = true,
    ValidIssuer = jwtOptions.Issuer,
    ValidAudience = jwtOptions.Audience,
    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
    ClockSkew = TimeSpan.Zero
};
```

#### 2. Bật/Tắt Kiểm tra định dạng Email chuẩn (chứa `@`)
📍 **[LoginQueryValidator.cs](file:///c:/code/abp_be/LegendsTeamVN.BadmintonClub/src/LegendsTeamVN.BadmintonClub.Application/Features/Auth/Login/LoginQueryValidator.cs#L9-L11)**
📍 **[RegisterCommandValidator.cs](file:///c:/code/abp_be/LegendsTeamVN.BadmintonClub/src/LegendsTeamVN.BadmintonClub.Application/Features/Auth/Register/RegisterCommandValidator.cs#L9-L11)**

Thêm lại `.EmailAddress()` nếu muốn bắt buộc phải nhập đúng cấu trúc email `@`:
```csharp
RuleFor(x => x.Email)
    .NotEmpty().WithMessage("Email is required.")
    .EmailAddress().WithMessage("Email is not in a valid format.");
```

#### 3. Bật/Tắt HTTPS Redirection
📍 **[Program.cs](file:///c:/code/abp_be/LegendsTeamVN.BadmintonClub/src/Hosts/LegendsTeamVN.BadmintonClub.API/Program.cs#L64-L67)**

Tắt trong môi trường Dev để các thiết bị cùng mạng LAN kết nối bằng HTTP cổng `54796` không bị dính HTTP 307 Redirect:
```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
```

---

## 📚 Tài liệu API (Swagger)

Khi API đang chạy, bạn có thể xem danh sách các API và test trực tiếp thông qua giao diện Swagger:
- **Đường dẫn Local**: `http://localhost:54796/swagger`
- **Đường dẫn LAN**: `http://<IP_MAY_BAN>:54796/swagger`

## 🏗️ Hướng dẫn Lập trình (Development Guidelines)

👉 **[Kiến trúc Hệ thống (Architecture)](docs/ARCHITECTURE.VI.md)**
👉 **[Thiết kế Cơ sở dữ liệu (Database)](docs/DATABASE.VI.md)**
👉 **[Hướng dẫn Lập trình API (API Guidelines)](docs/API_GUIDELINES.VI.md)**

## 📜 Giấy phép

Dự án này được cấp phép theo Giấy phép GPL-3.0 - xem chi tiết tại [LICENSE](LICENSE.txt).
