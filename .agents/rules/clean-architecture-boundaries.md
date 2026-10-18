---
trigger: always_on
description: "Bắt buộc tuân thủ ranh giới Clean Architecture: Nghiệp vụ lõi (Rich Domain Model) nằm trong Domain Entity, Ca sử dụng (Use Cases) nằm trong Application Handler, không viết logic nghiệp vụ trong Handler hoặc Endpoint."
globs: "**/*.cs"
---

# 🏛️ Phân Định Trách Nhiệm Tầng Clean Architecture (Domain vs Application)

Quy tắc này giải quyết triệt để vấn đề **"Đặt nhầm logic nghiệp vụ"** giữa các tầng. Tất cả AI Agent và kỹ sư phần mềm khi viết code trong dự án **LegendsTeamVN.BadmintonClub** bắt buộc phải tuân thủ ma trận phân định này.

---

## 1. Bản Đồ Trách Nhiệm Cốt Lõi (Core Responsibility Map)

```
[Presentation] ---> [Application (Use Cases)] ---> [Domain (Business Core)]
  Nhận request         Điều phối (Orchestrate)        Nghiệp vụ thuần khiết
  Validate format      Lấy/Lưu Repository            Bảo vệ tính toàn vẹn (Invariants)
  Gọi MediatR          Gửi Notification / Event       Tính toán, chuyển đổi trạng thái
```

---

## 2. Bảng Ma Trận: "Logic này phải đặt ở đâu?"

| Loại Logic | Thuộc Tầng | Giải thích chi tiết | Ví dụ cụ thể trong dự án |
| :--- | :---: | :--- | :--- |
| **Quy tắc chuyển đổi trạng thái (State Transition)** | **Domain** | Entity tự quyết định khi nào được đổi trạng thái và ai đổi. | `joinRequest.Approve(approverId)`<br>`session.Cancel(reason)` |
| **Bảo vệ tính toàn vẹn (Business Invariants)** | **Domain** | Không cho phép entity rơi vào trạng thái sai nghiệp vụ. | Kiểm tra: Buổi sinh hoạt đã kết thúc thì không được hủy, số người chơi không vượt quá MaxSlots. |
| **Tính toán nghiệp vụ (Business Calculations)** | **Domain** | Công thức tính tiền, chia tiền sân, tính điểm Elo, tính hội phí. | `feeRule.CalculateMemberFee(...)`<br>`match.CalculateScore(...)` |
| **Validation định dạng dữ liệu (Input Format)** | **Application** | Kiểm tra chuỗi rỗng, email hợp lệ, độ dài tối đa bằng FluentValidation. | `RuleFor(x => x.Name).NotEmpty()` trong `CreateClubCommandValidator` |
| **Điều phối luồng công việc (Orchestration)** | **Application** | Lấy dữ liệu từ Repo -> Gọi Entity xử lý -> Lưu lại Repo -> Gửi thông báo. | `ApproveJoinRequestCommandHandler` |
| **Kiểm tra quyền & Ngữ cảnh (Security/Context)** | **Application / Presentation** | Lấy UserId hiện tại, kiểm tra quyền hạn của người gọi. | `currentUserService.UserId`<br>`.RequireGroupPermission(...)` |
| **Giao tiếp dịch vụ bên ngoài (I/O & Third-party)**| **Application / Infra**| Gửi FCM Push Notification, gửi Email, gọi Cổng thanh toán. | `notificationService.SendAsync(...)` |
| **Truy vấn & Lưu trữ dữ liệu (SQL / EF Core)** | **Persistence** | Viết câu lệnh LINQ, Include bảng phụ, SaveChangesAsync. | `clubRepository.FindSingleAsync(...)` |

---

## 3. Quy Tắc Bất Biến Cho Từng Tầng

### 3.1. Tầng Domain: Rich Domain Model (CẤM Anemic Domain Model)
* **CẤM**: Entity chỉ có `{ get; set; }` rỗng tuếch và để Handler tự ý gán thuộc tính bên ngoài.
* **CẤM**: Domain phụ thuộc vào `DbContext`, Repository, hay bất kỳ I/O bên ngoài nào.
* **QUY CHUẨN**:
  * Các thuộc tính của Entity phải có `private set;` hoặc `protected set;`.
  * Mọi hành vi thay đổi dữ liệu phải thông qua **phương thức nghiệp vụ** của chính Entity.

```csharp
// ❌ SAI HOÀN TOÀN (Anemic Domain - logic bị tràn ra ngoài Handler):
joinRequest.Status = JoinRequestStatus.APPROVED;
joinRequest.ApprovedBy = approverId;
joinRequest.ReviewedAt = DateTime.UtcNow;

// ✅ ĐÚNG KIẾN TRÚC (Rich Domain Model - Entity tự quản lý nghiệp vụ):
joinRequest.Approve(approverId);
```

### 3.2. Tầng Application: Bộ Điều Phối (Pure Orchestrator)
* **CẤM**: Viết logic tính toán nghiệp vụ lõi, if/else kiểm tra quy tắc sinh hoạt CLB trong Handler.
* **CẤM**: Gọi trực tiếp SQL hay `DbContext`. Mọi truy cập phải qua Repository Interface.
* **QUY CHUẨN (Workflow chuẩn của một CommandHandler)**:
  1. *Lấy dữ liệu*: Gọi Repository để nạp Aggregate Root/Entity lên bộ nhớ.
  2. *Kiểm tra tồn tại*: Trả về `Result.Failure(Error.NotFound(...))` nếu không tìm thấy.
  3. *Uỷ quyền nghiệp vụ*: Gọi phương thức nghiệp vụ trên chính Entity (ví dụ: `club.AddSchedule(...)`).
  4. *Lưu trữ*: Gọi `repository.SaveChangesAsync()`.
  5. *Side-effects*: Kích hoạt Event hoặc Notification (nếu có).
  6. *Phản hồi*: Trả về `Result.Success(...)`.

```csharp
// ✅ KHUNG MẪU HANDLER CHUẨN (Chỉ điều phối, không ôm đồm nghiệp vụ):
public async Task<Result<Guid>> Handle(ApproveJoinRequestCommand request, CancellationToken cancellationToken)
{
    // 1. Kiểm tra ngữ cảnh
    var approverId = currentUserService.UserId;
    if (!approverId.HasValue) return Result.Failure<Guid>(Error.Unauthorized(...));

    // 2. Lấy Aggregate Root
    var joinRequest = await joinRequestRepository.FindByIdAsync(request.RequestId, cancellationToken);
    if (joinRequest is null) return Result.Failure<Guid>(Error.NotFound(...));

    // 3. Gọi nghiệp vụ thuần trên Domain Entity
    joinRequest.Approve(approverId.Value);

    // 4. Lưu thay đổi
    await joinRequestRepository.SaveChangesAsync(cancellationToken);

    return Result.Success(joinRequest.Id);
}
```

### 3.3. Tầng Presentation: Cổng Giao Tiếp (HTTP Gateway)
* **CẤM**: Bất kỳ logic kiểm tra điều kiện nghiệp vụ nào bên trong Minimal API Endpoints.
* **QUY CHUẨN**:
  * Endpoint chỉ nhận Request -> Map sang Command/Query -> Bắn qua MediatR `ISender.Send(...)` -> Ánh xạ `Result` sang HTTP Status Code (`Results.Ok`, `Results.Created`, `Results.NoContent`).
