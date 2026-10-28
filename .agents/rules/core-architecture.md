---
trigger: "src/LegendsTeamVN.BadmintonClub.Domain/**, src/LegendsTeamVN.BadmintonClub.Application/**, src/LegendsTeamVN.BadmintonClub.Persistence/**"
description: "Quy chuẩn tầng lõi Core (Domain, Application & Persistence): Rich Domain Model, CQRS Pure Orchestration, EF Core Mapping, và NetArchTest Compliance."
---

# Quy Chuẩn Tầng Lõi Hệ Thống (Core Architecture: Domain, Application & Persistence)

Tài liệu định nghĩa các quy chuẩn bắt buộc khi làm việc trong phân hệ Core: tầng Domain (nghiệp vụ thuần khiết), tầng Application (điều phối CQRS) và tầng Persistence (lưu trữ EF Core).

---

## 1. Bản Đồ Trách Nhiệm Cốt Lõi (Core Responsibility Map)

```text
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
| **Quy tắc chuyển đổi trạng thái (State Transition)** | **Domain** | Entity tự quyết định khi nào được đổi trạng thái, cập nhật timestamp, và người đổi. | `joinRequest.Approve(approverId)`<br>`session.Cancel(reason)`<br>`booking.CheckIn()` |
| **Bảo vệ tính toàn vẹn (Business Invariants)** | **Domain** | Không cho phép entity rơi vào trạng thái sai nghiệp vụ. Kiểm tra logic ngay trong method. | Buổi đã kết thúc thì không được hủy; số người không vượt quá MaxPlayers. |
| **Tính toán nghiệp vụ (Business Calculations)** | **Domain** | Công thức tính tiền, chia tiền sân, tính điểm Elo, tính hội phí, chiết khấu. | `feeRule.CalculateMemberFee(...)`<br>`session.CalculateTotalCost(...)` |
| **Validation định dạng dữ liệu (Input Format)** | **Application** | Kiểm tra chuỗi rỗng, email hợp lệ, regex, độ dài tối đa bằng FluentValidation. | `RuleFor(x => x.Name).NotEmpty()` trong `CreateClubCommandValidator` |
| **Điều phối luồng công việc (Orchestration)** | **Application** | Nạp Entity $\rightarrow$ Gọi method Entity $\rightarrow$ Lưu Repository $\rightarrow$ Bắn Event/SignalR. | `ApproveJoinRequestCommandHandler` |
| **Kiểm tra quyền & Ngữ cảnh (Security/Context)** | **Presentation & Application** | Kiểm tra JWT Claims, route parameter `groupId`, thẩm tra quyền hạn `GroupPermissions`. | `.RequireGroupPermission(...)`<br>`currentUserService.UserId` |
| **Giao tiếp dịch vụ ngoại vi (I/O & Third-party)** | **Application / Infra** | Gửi FCM Push Notification, gửi Email, gọi Cổng thanh toán, Redis cache. | `notificationService.SendAsync(...)` |
| **Truy vấn & Lưu trữ dữ liệu (SQL / EF Core)** | **Persistence** | Cấu hình EntityTypeConfiguration, LINQ, Include bảng phụ, SaveChangesAsync. | `clubRepository.FindSingleAsync(...)` |

---

## 3. Quy Tắc Bất Biến Tầng Domain: Rich Domain Model (CẤM Anemic Domain Model)

1. **Cấm Anemic Model:** Entity chỉ có `{ get; set; }` để Handler tự ý gán thuộc tính bên ngoài là vi phạm nghiêm trọng kiến trúc.
2. **Cấm Dependencies:** Domain không phụ thuộc vào `DbContext`, Repository, hay bất kỳ I/O bên ngoài nào.
3. **Cấm Public Setters:** 100% properties của Entity phải có `private set;` hoặc `protected set;`.
4. **Bắt Buộc Constructor Protected:** Bắt buộc có constructor không tham số `protected EntityName() { }` để ORM reflection hoạt động.
5. **Đóng Gói Hành Vi:** Mọi hành vi thay đổi dữ liệu phải thông qua phương thức nghiệp vụ của chính Entity.
6. **Domain Events Sealed:** Domain Events bắt buộc là `sealed class` và có hậu tố `DomainEvent`.

```csharp
// [FAIL] SAI HOÀN TOÀN (Anemic Domain - vi phạm NetArchTest và Clean Architecture):
public class SessionBooking : Entity<Guid>
{
    public BookingStatus Status { get; set; }
    public DateTime? CheckinAt { get; set; }
}
// Ngoài Handler tự ý gán:
booking.Status = BookingStatus.CHECKED_IN;
booking.CheckinAt = DateTime.UtcNow;

// [OK] ĐÚNG KIẾN TRÚC (Rich Domain Model - Đóng gói hoàn hảo):
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

---

## 4. Quy Tắc Bất Biến Tầng Application: Bộ Điều Phối Thuần Túy (Pure Orchestrator)

1. **Cấm Business Logic:** Viết logic tính toán nghiệp vụ, if/else kiểm tra quy tắc sinh hoạt CLB trong Handler là vi phạm.
2. **Cấm Raw SQL:** Không gọi trực tiếp SQL hay `BadmintonDbContext`. Mọi truy cập phải qua Repository Interface.
3. **Bắt Buộc Sealed:** Toàn bộ Handlers và Validators bắt buộc có từ khóa `sealed`.
4. **Quy Trình 6 Bước Chuẩn Trong Handler:**
   1. *Ngữ cảnh*: Lấy `currentUserService.UserId` hoặc xác thực đầu vào.
   2. *Nạp dữ liệu*: Gọi Repository để nạp Aggregate Root/Entity lên bộ nhớ.
   3. *Kiểm tra tồn tại*: Trả về `Result.Failure(Error.NotFound(...))` nếu không tìm thấy.
   4. *Ủy quyền nghiệp vụ*: Gọi phương thức nghiệp vụ trên chính Entity (ví dụ: `club.UpdateInfo(...)`).
   5. *Lưu trữ*: Gọi `repository.SaveChangesAsync()` hoặc UnitOfWork.
   6. *Phản hồi*: Trả về `Result.Success(...)`.

```csharp
// [OK] KHUNG MẪU HANDLER CHUẨN (Class sealed, Primary Constructor, Pure Orchestration):
public sealed class ApproveJoinRequestCommandHandler(
    IJoinRequestRepository joinRequestRepository,
    ICurrentUserService currentUserService) : ICommandHandler<ApproveJoinRequestCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ApproveJoinRequestCommand request, CancellationToken cancellationToken)
    {
        var joinRequest = await joinRequestRepository.FindByIdAsync(request.RequestId, cancellationToken);
        if (joinRequest is null)
            return Result.Failure<Guid>(Error.NotFound("JoinRequest.NotFound", "Yêu cầu gia nhập không tồn tại."));

        joinRequest.Approve(currentUserService.UserId);
        await joinRequestRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(joinRequest.Id);
    }
}
```

---

## 5. Quy Tắc Tầng Persistence: EF Core Configuration
- Cấu hình từng Entity thông qua file riêng kế thừa `IEntityTypeConfiguration<T>` tại `src/LegendsTeamVN.BadmintonClub.Persistence/Configurations/`.
- Tên bảng và tên cột khớp chính xác với CSDL 3NF trong [`docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md`](docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md).
- Toàn bộ navigation properties được cấu hình tường minh; không sử dụng lazy loading ngầm gây N+1 queries.
