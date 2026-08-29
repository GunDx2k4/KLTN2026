# Hướng Dẫn Triển Khai Luồng Đặt Sân & Thông Báo Firebase (FCM)

Tài liệu này hướng dẫn chi tiết quy trình triển khai chức năng **User đặt sân -> Chờ chủ sân duyệt -> Chủ sân duyệt/từ chối -> Gửi thông báo Push Notification về thiết bị của User** thông qua **Firebase Cloud Messaging (FCM)** trong dự án **LegendsTeamVN.BadmintonClub**.

---

## 1. Tổng Quan Quy Trình (Workflow)

```
[Khách Hàng (User)]           [Hệ Thống Backend]            [Chủ Sân (Owner)]          [Firebase FCM]
        │                              │                              │                      │
        │── 1. Đặt sân (Pending) ─────>│                              │                      │
        │                              │── 2. Báo có đơn mới ────────>│                      │
        │                              │   (Push FCM hoặc Topic)      │                      │
        │                              │                              │                      │
        │                              │<── 3. Duyệt đơn (Approved) ──│                      │
        │                              │                              │                      │
        │                              │── 4. Lưu DB & Gửi FCM ─────────────────────────────>│
        │                              │                                                     │
        │<── 5. Nhận Push Notification ──────────────────────────────────────────────────────│
        │    "Đơn đặt sân đã duyệt!"   │                                                     │
```

---

## 2. Quản Lý Device Token (FCM Token) Của User

Để gửi thông báo đến đúng điện thoại/trình duyệt của người dùng, hệ thống cần lưu **FCM Device Token** khi người dùng đăng nhập.

### 2.1. Thiết kế bảng lưu Token thiết bị (Tùy chọn khuyến nghị)

Tạo entity hoặc thêm trường lưu `DeviceToken` của User:

```csharp
// Domain/Entities/UserDeviceToken.cs
public class UserDeviceToken : SoftDeletableEntity<Guid>
{
    public Guid UserId { get; private set; }
    public string DeviceToken { get; private set; } = default!;
    public string? DeviceType { get; private set; } // "Android", "iOS", "Web"
    public DateTimeOffset LastUpdatedUtc { get; private set; }

    public UserDeviceToken(Guid userId, string deviceToken, string? deviceType = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        DeviceToken = deviceToken;
        DeviceType = deviceType;
        LastUpdatedUtc = DateTimeOffset.UtcNow;
    }

    public void UpdateToken(string newToken)
    {
        DeviceToken = newToken;
        LastUpdatedUtc = DateTimeOffset.UtcNow;
    }
}
```

---

## 3. Triển Khai Code Chi Tiết (Application & Presentation)

### 3.1. DTOs & Commands

#### A. Command Duyệt Đặt Sân: `ApproveBookingCommand.cs`
📁 `src/LegendsTeamVN.BadmintonClub.Application/Features/Bookings/Approve/ApproveBookingCommand.cs`

```csharp
using LegendsTeamVN.Core.Application.Messaging;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Approve;

public record ApproveBookingCommand(Guid BookingId, string? Note = null) : ICommand<bool>;
```

#### B. Command Từ Chối Đặt Sân: `RejectBookingCommand.cs`
📁 `src/LegendsTeamVN.BadmintonClub.Application/Features/Bookings/Reject/RejectBookingCommand.cs`

```csharp
using LegendsTeamVN.Core.Application.Messaging;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Reject;

public record RejectBookingCommand(Guid BookingId, string Reason) : ICommand<bool>;
```

---

### 3.2. Command Handlers Xử Lý Nghiệp Vụ & Gửi Thông Báo

#### A. Handler Duyệt Đặt Sân: `ApproveBookingCommandHandler.cs`
📁 `src/LegendsTeamVN.BadmintonClub.Application/Features/Bookings/Approve/ApproveBookingCommandHandler.cs`

```csharp
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Approve;

public class ApproveBookingCommandHandler : ICommandHandler<ApproveBookingCommand, bool>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserDeviceTokenRepository _deviceTokenRepository; // Lấy token của user
    private readonly INotificationService _firebaseNotificationService;
    private readonly ILogger<ApproveBookingCommandHandler> _logger;

    public ApproveBookingCommandHandler(
        IBookingRepository bookingRepository,
        INotificationRepository notificationRepository,
        IUserDeviceTokenRepository deviceTokenRepository,
        INotificationService firebaseNotificationService,
        ILogger<ApproveBookingCommandHandler> logger)
    {
        _bookingRepository = bookingRepository;
        _notificationRepository = notificationRepository;
        _deviceTokenRepository = deviceTokenRepository;
        _firebaseNotificationService = firebaseNotificationService;
        _logger = logger;
    }

    public async Task<bool> Handle(ApproveBookingCommand request, CancellationToken cancellationToken)
    {
        // 1. Tìm đơn đặt sân
        var booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy đơn đặt sân với mã: {request.BookingId}");
        }

        if (booking.Status != BookingStatus.Pending)
        {
            throw new InvalidOperationException("Chỉ có thể duyệt đơn đang ở trạng thái Chờ duyệt (Pending)!");
        }

        // 2. Cập nhật trạng thái Booking sang Confirmed / Approved
        booking.UpdateStatus(BookingStatus.Confirmed);
        await _bookingRepository.UpdateAsync(booking, cancellationToken);

        // 3. Lưu bản ghi thông báo vào Database (để User xem trong ứng dụng)
        var notificationTitle = "Đơn đặt sân đã được duyệt! 🎉";
        var notificationBody = $"Đơn đặt sân #{booking.Id.ToString()[..8]} của bạn đã được chủ sân xác nhận thành công.";
        
        var dbNotification = new Notification(booking.UserId, notificationTitle, notificationBody);
        await _notificationRepository.AddAsync(dbNotification, cancellationToken);

        // 4. Lấy danh sách Device Token của User và gửi Push Notification qua Firebase
        var deviceTokens = await _deviceTokenRepository.GetTokensByUserIdAsync(booking.UserId, cancellationToken);

        if (deviceTokens != null && deviceTokens.Count > 0)
        {
            var notificationData = new Dictionary<string, string>
            {
                { "bookingId", booking.Id.ToString() },
                { "type", "BOOKING_APPROVED" },
                { "click_action", "FLUTTER_NOTIFICATION_CLICK" }
            };

            try
            {
                // Gửi thông báo đến tất cả thiết bị của User
                await _firebaseNotificationService.SendMulticastNotificationAsync(
                    deviceTokens,
                    notificationTitle,
                    notificationBody,
                    notificationData,
                    cancellationToken);

                _logger.LogInformation("Đã gửi push notification duyệt đơn cho User: {UserId}", booking.UserId);
            }
            catch (Exception ex)
            {
                // Không làm gián đoạn flow duyệt đơn nếu push notification gặp lỗi mạng/token hết hạn
                _logger.LogError(ex, "Lỗi khi gửi Firebase Notification cho User: {UserId}", booking.UserId);
            }
        }

        return true;
    }
}
```

---

#### B. Handler Từ Chối Đặt Sân: `RejectBookingCommandHandler.cs`
📁 `src/LegendsTeamVN.BadmintonClub.Application/Features/Bookings/Reject/RejectBookingCommandHandler.cs`

```csharp
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Reject;

public class RejectBookingCommandHandler : ICommandHandler<RejectBookingCommand, bool>
{
    private readonly IBookingRepository _bookingRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserDeviceTokenRepository _deviceTokenRepository;
    private readonly INotificationService _firebaseNotificationService;
    private readonly ILogger<RejectBookingCommandHandler> _logger;

    public RejectBookingCommandHandler(
        IBookingRepository bookingRepository,
        INotificationRepository notificationRepository,
        IUserDeviceTokenRepository deviceTokenRepository,
        INotificationService firebaseNotificationService,
        ILogger<RejectBookingCommandHandler> logger)
    {
        _bookingRepository = bookingRepository;
        _notificationRepository = notificationRepository;
        _deviceTokenRepository = deviceTokenRepository;
        _firebaseNotificationService = firebaseNotificationService;
        _logger = logger;
    }

    public async Task<bool> Handle(RejectBookingCommand request, CancellationToken cancellationToken)
    {
        var booking = await _bookingRepository.GetByIdAsync(request.BookingId, cancellationToken);
        if (booking == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy đơn đặt sân với mã: {request.BookingId}");
        }

        // Cập nhật trạng thái hủy / từ chối
        booking.UpdateStatus(BookingStatus.Cancelled);
        await _bookingRepository.UpdateAsync(booking, cancellationToken);

        // Lưu thông báo Database
        var title = "Đơn đặt sân bị từ chối";
        var body = $"Đơn đặt sân #{booking.Id.ToString()[..8]} đã bị từ chối. Lý do: {request.Reason}";
        
        var dbNotification = new Notification(booking.UserId, title, body);
        await _notificationRepository.AddAsync(dbNotification, cancellationToken);

        // Gửi Firebase Push Notification
        var deviceTokens = await _deviceTokenRepository.GetTokensByUserIdAsync(booking.UserId, cancellationToken);
        if (deviceTokens != null && deviceTokens.Count > 0)
        {
            var data = new Dictionary<string, string>
            {
                { "bookingId", booking.Id.ToString() },
                { "type", "BOOKING_REJECTED" },
                { "reason", request.Reason }
            };

            await _firebaseNotificationService.SendMulticastNotificationAsync(
                deviceTokens, 
                title, 
                body, 
                data, 
                cancellationToken);
        }

        return true;
    }
}
```

---

### 3.3. Định Tuyến API Endpoint (Carter Minimal API)

📁 `src/LegendsTeamVN.BadmintonClub.Presentation/Endpoints/Bookings/BookingEndpoints.cs`

```csharp
using Carter;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Approve;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Reject;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints.Bookings;

public class BookingEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/bookings")
                       .WithTags("Bookings");

        // 1. Chủ sân duyệt đơn đặt sân
        group.MapPost("/{id:guid}/approve", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new ApproveBookingCommand(id));
            return Results.Ok(new { Success = result, Message = "Duyệt đơn đặt sân thành công." });
        })
        .RequireAuthorization()
        .WithName("ApproveBooking")
        .WithSummary("Chủ sân duyệt đơn đặt sân và gửi thông báo FCM về User");

        // 2. Chủ sân từ chối đơn đặt sân
        group.MapPost("/{id:guid}/reject", async (Guid id, RejectBookingRequest request, ISender sender) =>
        {
            var result = await sender.Send(new RejectBookingCommand(id, request.Reason));
            return Results.Ok(new { Success = result, Message = "Đã từ chối đơn đặt sân." });
        })
        .RequireAuthorization()
        .WithName("RejectBooking")
        .WithSummary("Chủ sân từ chối đơn đặt sân và gửi thông báo FCM về User");
    }
}

public record RejectBookingRequest(string Reason);
```

---

## 4. Kịch Bản Kiểm Thử Thực Tế (Testing Guide)

### Bước 1: Lấy FCM Token từ thiết bị Client (Web/Mobile App)
Trên ứng dụng Client (Flutter, React Native hoặc Web), sau khi xin quyền Notification:
```javascript
// Ví dụ trên Web/JS
import { getToken } from "firebase/messaging";
const fcmToken = await getToken(messaging, { vapidKey: "YOUR_VAPID_KEY" });
console.log("Device FCM Token:", fcmToken);
```

### Bước 2: Test gửi thông báo trực tiếp qua Swagger/Postman
Gọi API duyệt đơn đặt sân:
```http
POST /api/v1/bookings/3fa85f64-5717-4562-b3fc-2c963f66afa6/approve
Authorization: Bearer <OWNER_JWT_TOKEN>
```

### Bước 3: Kết quả trên điện thoại / trình duyệt của khách hàng
- **Khi ứng dụng đang chạy nền / tắt màn hình:** Xuất hiện System Push Notification trên thanh trạng thái điện thoại với Tiêu đề: `Đơn đặt sân đã được duyệt! 🎉`.
- **Khi nhấn vào thông báo:** Payload `bookingId` và `type: "BOOKING_APPROVED"` trong trường `Data` sẽ điều hướng màn hình người dùng đến chi tiết đơn đặt sân.
