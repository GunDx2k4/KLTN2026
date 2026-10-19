---
trigger: always_on
description: "Bắt buộc tuân thủ ranh giới Clean Architecture 4 tầng, Rich Domain Model (CẤM Anemic Model, private set, parameterless ctor), CQRS Pure Orchestration (sealed handlers, sealed validators), và Minimal API Endpoints."
---

# 🏛️ Phân Định Trách Nhiệm Tầng Clean Architecture (Domain vs Application vs Presentation)

> **MỤC TIÊU**: Ngăn chặn triệt để tình trạng **"Đặt nhầm logic nghiệp vụ"** và **"Phá vỡ ranh giới phân tầng"** (Boundary Leakage). Toàn bộ AI Agent và kỹ sư phần mềm khi viết code trong dự án **LegendsTeamVN.BadmintonClub** bắt buộc phải tuân thủ nghiêm ngặt ma trận và các quy tắc dưới đây.

---

## 1. Bản Đồ Trách Nhiệm Cốt Lõi (Core Responsibility Map)

```
[Presentation] ───► [Application (Use Cases)] ───► [Domain (Business Core)]
  - Nhận HTTP Request   - Điều phối luồng (Orchestrate)   - Nghiệp vụ thuần khiết
  - Format Response      - Nạp/Lưu Repository              - Bảo vệ tính toàn vẹn (Invariants)
  - RBAC Route Guard    - Gửi Event/Notification          - Chuyển đổi trạng thái (State Transition)
  - Gọi MediatR Send     - Kiểm tra quyền & Idempotency    - Tính toán tiền bạc/điểm số nội bộ
```

---

## 2. Bảng Ma Trận: "Logic này phải đặt ở đâu?"

| Loại Logic | Thuộc Tầng | Giải thích chi tiết & Trách nhiệm | Ví dụ cụ thể trong dự án |
| :--- | :---: | :--- | :--- |
| **Quy tắc chuyển đổi trạng thái (State Transition)** | **Domain** | Entity tự quyết định khi nào được đổi trạng thái, cập nhật timestamp, và ai đổi. | `joinRequest.Approve(approverId)`<br>`session.Cancel(reason)`<br>`booking.CheckIn()` |
| **Bảo vệ tính toàn vẹn (Business Invariants)** | **Domain** | Không cho phép entity rơi vào trạng thái sai nghiệp vụ. Kiểm tra logic ngay trong method. | Kiểm tra: Buổi đã kết thúc thì không được hủy; số người không vượt quá MaxPlayers. |
| **Tính toán nghiệp vụ (Business Calculations)** | **Domain** | Công thức tính tiền, chia tiền sân, tính điểm Elo, tính hội phí, chiết khấu. | `feeRule.CalculateMemberFee(...)`<br>`session.CalculateTotalCost(...)` |
| **Validation định dạng dữ liệu (Input Format)** | **Application** | Kiểm tra chuỗi rỗng, email hợp lệ, regex, độ dài tối đa bằng FluentValidation. | `RuleFor(x => x.Name).NotEmpty()` trong `CreateClubCommandValidator` |
| **Điều phối luồng công việc (Orchestration)** | **Application** | Nạp Entity -> Gọi method Entity -> Lưu Repository -> Bắn Event/SignalR. | `ApproveJoinRequestCommandHandler` |
| **Kiểm tra quyền & Ngữ cảnh (Security/Context)** | **Presentation & Application** | Kiểm tra JWT Claims, route parameter `groupId`, thẩm tra quyền hạn `GroupPermissions`. | `.RequireGroupPermission(...)`<br>`currentUserService.UserId` |
| **Giao tiếp dịch vụ ngoại vi (I/O & Third-party)**| **Application / Infra**| Gửi FCM Push Notification, gửi Email, gọi Cổng thanh toán, Redis cache. | `notificationService.SendAsync(...)` |
| **Truy vấn & Lưu trữ dữ liệu (SQL / EF Core)** | **Persistence** | Cấu hình EntityTypeConfiguration, LINQ, Include bảng phụ, SaveChangesAsync. | `clubRepository.FindSingleAsync(...)` |

---

## 3. Quy Tắc Bất Biến & Chống Lỗi Kiến Trúc (Anti-Patterns)

### 3.1. Tầng Domain: Rich Domain Model (CẤM Anemic Domain Model)
* **CẤM**: Entity chỉ có `{ get; set; }` để Handler tự ý gán thuộc tính bên ngoài.
* **CẤM**: Domain phụ thuộc vào `DbContext`, Repository, hay bất kỳ I/O bên ngoài nào.
* **CẤM**: Public setters trên các thuộc tính của Entity.
* **BẮT BUỘC**:
  * Các thuộc tính của Entity phải có `private set;` hoặc `protected set;`.
  * Bắt buộc có **parameterless constructor** (`private` hoặc `protected`) để ORM reflection hoạt động.
  * Mọi hành vi thay đổi dữ liệu phải thông qua **phương thức nghiệp vụ** của chính Entity.
  * Domain Events bắt buộc là `sealed class` và có hậu tố `DomainEvent`.

```csharp
// ❌ SAI HOÀN TOÀN (Anemic Domain - vi phạm NetArchTest và Clean Architecture):
public class SessionBooking : Entity<Guid>
{
    public BookingStatus Status { get; set; }
    public DateTime? CheckinAt { get; set; }
}
// Ngoài Handler tự ý gán:
booking.Status = BookingStatus.CHECKED_IN;
booking.CheckinAt = DateTime.UtcNow;

// ✅ ĐÚNG KIẾN TRÚC (Rich Domain Model - Đóng gói hoàn hảo):
public class SessionBooking : Entity<Guid>
{
    public BookingStatus Status { get; private set; }
    public DateTime? CheckinAt { get; private set; }

    protected SessionBooking() { } // Bắt buộc cho ORM Reflection

    public void CheckIn()
    {
        if (Status != BookingStatus.CONFIRMED)
            throw new DomainException("Chỉ người chơi có vé CONFIRMED mới được điểm danh.");

        Status = BookingStatus.CHECKED_IN;
        CheckinAt = DateTime.UtcNow;
    }
}
```

### 3.2. Tầng Application: Bộ Điều Phối Thuần Túy (Pure Orchestrator)
* **CẤM**: Viết logic tính toán nghiệp vụ lõi, if/else kiểm tra quy tắc sinh hoạt CLB trong Handler.
* **CẤM**: Gọi trực tiếp SQL hay `BadmintonDbContext`. Mọi truy cập phải qua Repository Interface.
* **CẤM**: Quên từ khóa `sealed` trên Handler hoặc Validator.
* **BẮT BUỘC (Quy trình 6 bước trong Handler)**:
  1. *Ngữ cảnh*: Kiểm tra `currentUserService.UserId` hoặc xác thực đầu vào.
  2. *Lấy dữ liệu*: Gọi Repository để nạp Aggregate Root/Entity lên bộ nhớ.
  3. *Kiểm tra tồn tại*: Trả về `Result.Failure(Error.NotFound(...))` nếu không tìm thấy.
  4. *Ủy quyền nghiệp vụ*: Gọi phương thức nghiệp vụ trên chính Entity (ví dụ: `club.UpdateInfo(...)`).
  5. *Lưu trữ*: Gọi `repository.SaveChangesAsync()`.
  6. *Phản hồi*: Trả về `Result.Success(...)`.

```csharp
// ✅ KHUNG MẪU HANDLER CHUẨN (Class sealed, Primary Constructor, Pure Orchestration):
public sealed class ApproveJoinRequestCommandHandler(
    IJoinRequestRepository joinRequestRepository,
    ICurrentUserService currentUserService) : ICommandHandler<ApproveJoinRequestCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ApproveJoinRequestCommand request, CancellationToken cancellationToken)
    {
        var approverId = currentUserService.UserId;
        if (!approverId.HasValue)
            return Result.Failure<Guid>(Error.Unauthorized("Auth.Unauthorized", "Yêu cầu đăng nhập."));

        var joinRequest = await joinRequestRepository.FindByIdAsync(request.RequestId, cancellationToken);
        if (joinRequest is null)
            return Result.Failure<Guid>(Error.NotFound("JoinRequest.NotFound", "Không tìm thấy yêu cầu gia nhập."));

        // Gọi phương thức nghiệp vụ thuần trên Domain Entity
        joinRequest.Approve(approverId.Value);

        await joinRequestRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(joinRequest.Id);
    }
}
```

### 3.3. Tầng Presentation: Cổng Giao Tiếp (HTTP Gateway)
* **CẤM**: Viết logic kiểm tra điều kiện nghiệp vụ hoặc tính toán số tiền bên trong Endpoint.
* **CẤM**: Inject Repository hoặc DbContext vào Endpoint.
* **CẤM**: Quên hậu tố `Endpoint` hoặc quên kế thừa `EndpointGroupBase`.
* **CẤM**: Quên gắn `.RequireGroupPermission(...)` cho các endpoint câu lạc bộ.
* **BẮT BUỘC**:
  * Endpoint chỉ nhận Request -> Map sang Command/Query -> Gửi qua `ISender.Send(...)` -> Ánh xạ `Result` sang HTTP Status Code bằng `result.Match(...)`.

---

## 4. Ma Trận Đối Soát NetArchTest (Automatic Guardrails)

| Kiểm Tra Kiến Trúc | Bài Test NetArchTest | Nếu Vi Phạm Sẽ Xảy Ra |
| :--- | :--- | :--- |
| **Domain không phụ thuộc tầng khác** | `DomainLayer_ShouldNotHaveDependencyOn_*` | Test FAIL: Có `using Application/Persistence...` trong Domain |
| **Entity có parameterless constructor** | `Entities_ShouldHave_ParameterlessConstructor` | Test FAIL: Entity thiếu `protected EntityName() { }` |
| **Entity không có public setter** | `Entities_ShouldHave_PrivateSetters` | Test FAIL: Có thuộc tính dùng `public set;` |
| **Domain Event phải sealed và có hậu tố**| `DomainEvents_Should_BeSealed` & `Postfix` | Test FAIL: Domain event thiếu `sealed` hoặc thiếu chữ `DomainEvent` |
| **Application không phụ thuộc tầng ngoài** | `ApplicationLayer_ShouldNotHaveDependencyOn_*` | Test FAIL: Có `using Persistence/Infrastructure...` trong Application |
| **Handler phải sealed và có hậu tố** | `CommandHandlers_ShouldBe_Sealed` & `Postfix` | Test FAIL: Handler thiếu `sealed` hoặc tên sai quy ước |
| **Validator phải sealed và có hậu tố** | `Validators_ShouldBe_Sealed` & `Postfix` | Test FAIL: Validator thiếu `sealed` hoặc thiếu chữ `Validator` |
| **Presentation không phụ thuộc Persistence**| `PresentationLayer_ShouldNotHaveDependencyOn_*`| Test FAIL: Endpoint inject DbContext hoặc Repository |
| **Endpoint phải kế thừa và có hậu tố** | `Endpoints_ShouldHave_EndpointPostfix` | Test FAIL: Class thiếu hậu tố `Endpoint` |
