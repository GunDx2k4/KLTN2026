# Quy Trình Triển Khai Một Ca Sử Dụng Chuẩn Kiến Trúc Từ Docs Đến Code

Dưới đây là ví dụ minh họa cách nhận diện và hiện thực hóa một Use Case bất kỳ từ `docs/` mà không bị sai lệch đặc tả:

## Bước 1: Tra Cứu Đặc Tả Bằng Script (Trace SSOT)
```powershell
python .agents/skills/implement-system-spec/scripts/trace_system_spec.py --fr FR14
```
**Kết quả thu được từ docs/:**
- Mã UC: `UC12 - Điểm danh thành viên tại sân`
- Bảng CSDL: `session_booking`, `badminton_session`
- Route: `POST /api/v1/clubs/{groupId}/sessions/{sessionId}/check-in`
- Quyền: `GroupPermissions.Operations.MatchManage`

## Bước 2: Kiểm Tra & Cập Nhật Tầng Domain (Domain Layer)
Trong `SessionBooking.cs`:
```csharp
public void CheckIn(Guid operatorUserId)
{
    if (Status != BookingStatus.CONFIRMED)
        throw new DomainException($"Chỉ người chơi có vé CONFIRMED mới được điểm danh. Trạng thái hiện tại: {Status}.");

    Status = BookingStatus.CHECKED_IN;
    CheckinAt = DateTime.UtcNow;
    ModifiedBy = operatorUserId;
    ModifiedOnUtc = DateTime.UtcNow;

    AddDomainEvent(new PlayerCheckedInDomainEvent(SessionId, UserId, CheckinAt.Value));
}
```
*Bất biến*: Thuộc tính `Status` và `CheckinAt` có `private set;`, Entity có `protected SessionBooking() { }`.

## Bước 3: Triển Khai Tầng Application (CQRS Vertical Slice)
Sử dụng skill `add-cqrs-feature`:
```powershell
python .agents/skills/add-cqrs-feature/scripts/scaffold_cqrs_slice.py --type command --name CheckInPlayer --module Sessions
```
Viết Handler phối hợp (Pure Orchestration):
1. Nạp `SessionBooking` theo `(sessionId, userId)`.
2. Kiểm tra tồn tại.
3. Gọi `booking.CheckIn(operatorId)`.
4. Gọi `bookingRepository.SaveChangesAsync()`.
5. Trả về `Result.Success(booking.Id)`.

## Bước 4: Triển Khai Tầng Presentation (Minimal API Endpoint)
Kế thừa `EndpointGroupBase`, gắn route guard:
```csharp
group.MapPost("{groupId:guid}/sessions/{sessionId:guid}/check-in", CheckInPlayer)
    .WithName("CheckInPlayer")
    .WithSummary("Điểm danh người chơi có mặt tại sân")
    .RequireGroupPermission(GroupPermissions.Operations.MatchManage);
```

## Bước 5: Kiểm Thử Kiến Trúc (Architecture Verification)
```powershell
python .agents/skills/verify-architecture/scripts/verify_architecture.py
```
Đảm bảo kết quả: `[OK] Architecture Tests Passed: 37/37 (100%)`.

## Bước 6: Đề Xuất Git Commit
Trình bày message cho người dùng:
```text
feat(session): triển khai tính năng điểm danh người chơi tại sân fr14
```
