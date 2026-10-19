---
trigger: glob
globs: "src/**, tests/**"
description: "Clean Architecture 4-tier boundaries, Rich Domain Model vs Pure Orchestration, Entity encapsulation, and NetArchTest compliance."
---

# Phan Dinh Trach Nhiem Tang Clean Architecture (Domain vs Application vs Presentation)

> [!IMPORTANT]
> Ngan chan triet de tinh trang "Dat nham logic nghiep vu" va "Pha vo ranh gioi phan tang" (Boundary Leakage). Toan bo AI Agent va ky su phan mem khi viet code trong du an LegendsTeamVN.BadmintonClub bat buoc phai tuan thu nghiem ngat ma tran va cac quy tac duoi day.

---

## 1. Ban Do Trach Nhiem Cot Loi (Core Responsibility Map)

```
[Presentation] ───► [Application (Use Cases)] ───► [Domain (Business Core)]
  - Nhan HTTP Request   - Dieu phoi luong (Orchestrate)   - Nghiep vu thuan khiet
  - Format Response      - Nap/Luu Repository              - Bao ve tinh toan ven (Invariants)
  - RBAC Route Guard    - Gui Event/Notification          - Chuyen doi trang thai (State Transition)
  - Goi MediatR Send     - Kiem tra quyen & Idempotency    - Tinh toan tien bac/diem so noi bo
```

---

## 2. Bang Ma Tran: "Logic nay phai dat o dau?"

| Loai Logic | Thuoc Tang | Giai thich chi tiet & Trach nhiem | Vi du cu the trong du an |
| :--- | :---: | :--- | :--- |
| **Quy tac chuyen doi trang thai (State Transition)** | **Domain** | Entity tu quyet dinh khi nao duoc doi trang thai, cap nhat timestamp, va ai doi. | `joinRequest.Approve(approverId)`<br>`session.Cancel(reason)`<br>`booking.CheckIn()` |
| **Bao ve tinh toan ven (Business Invariants)** | **Domain** | Khong cho phep entity roi vao trang thai sai nghiep vu. Kiem tra logic ngay trong method. | Kiem tra: Buoi da ket thuc thi khong duoc huy; so nguoi khong vuot qua MaxPlayers. |
| **Tinh toan nghiep vu (Business Calculations)** | **Domain** | Cong thuc tinh tien, chia tien san, tinh diem Elo, tinh hoi phi, chiet khau. | `feeRule.CalculateMemberFee(...)`<br>`session.CalculateTotalCost(...)` |
| **Validation dinh dang du lieu (Input Format)** | **Application** | Kiem tra chuoi rong, email hop le, regex, do dai toi da bang FluentValidation. | `RuleFor(x => x.Name).NotEmpty()` trong `CreateClubCommandValidator` |
| **Dieu phoi luong cong viec (Orchestration)** | **Application** | Nap Entity -> Goi method Entity -> Luu Repository -> Ban Event/SignalR. | `ApproveJoinRequestCommandHandler` |
| **Kiem tra quyen & Ngu canh (Security/Context)** | **Presentation & Application** | Kiem tra JWT Claims, route parameter `groupId`, tham tra quyen han `GroupPermissions`. | `.RequireGroupPermission(...)`<br>`currentUserService.UserId` |
| **Giao tiep dich vu ngoai vi (I/O & Third-party)**| **Application / Infra**| Gui FCM Push Notification, gui Email, goi Cong thanh toan, Redis cache. | `notificationService.SendAsync(...)` |
| **Truy van & Luu tru du lieu (SQL / EF Core)** | **Persistence** | Cau hinh EntityTypeConfiguration, LINQ, Include bang phu, SaveChangesAsync. | `clubRepository.FindSingleAsync(...)` |

---

## 3. Quy Tac Bat Bien & Chong Loi Kien Truc (Anti-Patterns)

### 3.1. Tang Domain: Rich Domain Model (CAM Anemic Domain Model)
- **CAM:** Entity chi co `{ get; set; }` de Handler tu y gan thuoc tinh ben ngoai.
- **CAM:** Domain phu thuoc vao `DbContext`, Repository, hay bat ky I/O ben ngoai nao.
- **CAM:** Public setters tren cac thuoc tinh cua Entity.
- **BAT BUOC:**
  - Cac thuoc tinh cua Entity phai co `private set;` hoac `protected set;`.
  - Bat buoc co **parameterless constructor** (`private` hoac `protected`) de ORM reflection hoat dong.
  - Moi hanh vi thay doi du lieu phai thong qua **phuong thuc nghiep vu** cua chinh Entity.
  - Domain Events bat buoc la `sealed class` va co hau to `DomainEvent`.

```csharp
// [FAIL] SAI HOAN TOAN (Anemic Domain - vi pham NetArchTest va Clean Architecture):
public class SessionBooking : Entity<Guid>
{
    public BookingStatus Status { get; set; }
    public DateTime? CheckinAt { get; set; }
}
// Ngoai Handler tu y gan:
booking.Status = BookingStatus.CHECKED_IN;
booking.CheckinAt = DateTime.UtcNow;

// [OK] DUNG KIEN TRUC (Rich Domain Model - Dong goi hoan hao):
public class SessionBooking : Entity<Guid>
{
    public BookingStatus Status { get; private set; }
    public DateTime? CheckinAt { get; private set; }

    protected SessionBooking() { } // Bat buoc cho ORM Reflection

    public void CheckIn()
    {
        if (Status != BookingStatus.CONFIRMED)
            throw new DomainException("Chi nguoi choi co ve CONFIRMED moi duoc diem danh.");

        Status = BookingStatus.CHECKED_IN;
        CheckinAt = DateTime.UtcNow;
    }
}
```

### 3.2. Tang Application: Bo Dieu Phoi Thuan Tuy (Pure Orchestrator)
- **CAM:** Viet logic tinh toan nghiep vu loi, if/else kiem tra quy tac sinh hoat CLB trong Handler.
- **CAM:** Goi truc tiep SQL hay `BadmintonDbContext`. Moi truy cap phai qua Repository Interface.
- **CAM:** Quen tu khoa `sealed` tren Handler hoac Validator.
- **BAT BUOC (Quy trinh 6 buoc trong Handler):**
  1. *Ngu canh*: Kiem tra `currentUserService.UserId` hoac xac thuc dau vao.
  2. *Lay du lieu*: Goi Repository de nap Aggregate Root/Entity len bo nho.
  3. *Kiem tra ton tai*: Tra ve `Result.Failure(Error.NotFound(...))` neu khong tim thay.
  4. *Uy quyen nghiep vu*: Goi phuong thuc nghiep vu tren chinh Entity (vi du: `club.UpdateInfo(...)`).
  5. *Luu tru*: Goi `repository.SaveChangesAsync()`.
  6. *Phan hoi*: Tra ve `Result.Success(...)`.

```csharp
// [OK] KHUNG MAU HANDLER CHUAN (Class sealed, Primary Constructor, Pure Orchestration):
public sealed class ApproveJoinRequestCommandHandler(
    IJoinRequestRepository joinRequestRepository,
    ICurrentUserService currentUserService) : ICommandHandler<ApproveJoinRequestCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ApproveJoinRequestCommand request, CancellationToken cancellationToken)
    {
        var approverId = currentUserService.UserId;
        if (!approverId.HasValue)
            return Result.Failure<Guid>(Error.Unauthorized("Auth.Unauthorized", "Yeu cau dang nhap."));

        var joinRequest = await joinRequestRepository.FindByIdAsync(request.RequestId, cancellationToken);
        if (joinRequest is null)
            return Result.Failure<Guid>(Error.NotFound("JoinRequest.NotFound", "Khong tim thay yeu cau gia nhap."));

        // Goi phuong thuc nghiep vu thuan tren Domain Entity
        joinRequest.Approve(approverId.Value);

        await joinRequestRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(joinRequest.Id);
    }
}
```

### 3.3. Tang Presentation: Cong Giao Tiep (HTTP Gateway)
- **CAM:** Viet logic kiem tra dieu kien nghiep vu hoac tinh toan so tien ben trong Endpoint.
- **CAM:** Inject Repository hoac DbContext vao Endpoint.
- **CAM:** Quen hau to `Endpoint` hoac quen ke thua `EndpointGroupBase`.
- **CAM:** Quen gan `.RequireGroupPermission(...)` cho cac endpoint cau lac bo.
- **BAT BUOC:**
  - Endpoint chi nhan Request -> Map sang Command/Query -> Gui qua `ISender.Send(...)` -> Anh xa `Result` sang HTTP Status Code bang `result.Match(...)`.

---

## 4. Ma Tran Doi Soat NetArchTest (Automatic Guardrails)

| Kiem Tra Kien Truc | Bai Test NetArchTest | Neu Vi Pham Se Xay Ra |
| :--- | :--- | :--- |
| **Domain khong phu thuoc tang khac** | `DomainLayer_ShouldNotHaveDependencyOn_*` | Test FAIL: Co `using Application/Persistence...` trong Domain |
| **Entity co parameterless constructor** | `Entities_ShouldHave_ParameterlessConstructor` | Test FAIL: Entity thieu `protected EntityName() { }` |
| **Entity khong co public setter** | `Entities_ShouldHave_PrivateSetters` | Test FAIL: Co thuoc tinh dung `public set;` |
| **Domain Event phai sealed va co hau to**| `DomainEvents_Should_BeSealed` & `Postfix` | Test FAIL: Domain event thieu `sealed` hoac thieu chu `DomainEvent` |
| **Application khong phu thuoc tang ngoai** | `ApplicationLayer_ShouldNotHaveDependencyOn_*` | Test FAIL: Co `using Persistence/Infrastructure...` trong Application |
| **Handler phai sealed va co hau to** | `CommandHandlers_ShouldBe_Sealed` & `Postfix` | Test FAIL: Handler thieu `sealed` hoac ten sai quy uoc |
| **Validator phai sealed va co hau to** | `Validators_ShouldBe_Sealed` & `Postfix` | Test FAIL: Validator thieu `sealed` hoac thieu chu `Validator` |
| **Presentation khong phu thuoc Persistence**| `PresentationLayer_ShouldNotHaveDependencyOn_*`| Test FAIL: Endpoint inject DbContext hoac Repository |
| **Endpoint phai ke thua va co hau to** | `Endpoints_ShouldHave_EndpointPostfix` | Test FAIL: Class thieu hau to `Endpoint` |
