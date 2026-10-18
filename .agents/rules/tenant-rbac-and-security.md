# 🛡️ Quy Tắc Phân Quyền Đa Nhóm Động (Dynamic Contextual Multi-Group RBAC)

Tài liệu này định nghĩa các quy tắc kiểm soát quyền hạn và ngữ cảnh đa nhóm bắt buộc AI Agent phải tuân thủ trong toàn bộ dự án **LegendsTeamVN.BadmintonClub**.

---

## 1. Bản Chất Nghiệp Vụ Cốt Lõi: 1 User — Nhiều Nhóm — Nhiều Vai Trò

Khác với mô hình Multi-Tenancy truyền thống của SaaS doanh nghiệp (gán cứng người dùng vào một Tenant duy nhất), hệ thống cầu lông phong trào hoạt động theo mô hình **Đa Nhóm Linh Hoạt (Multi-Club Ecosystem)**:

```
                          ┌────────────────────────┐
                          │   AppUser (Global)     │
                          │  (UserId: 8a4c-...-f1)  │
                          └───────────┬────────────┘
                                      │
            ┌─────────────────────────┼─────────────────────────┐
            ▼                         ▼                         ▼
   [CLB Cầu Lông Hà Đông]     [CLB Cầu Lông Ba Đình]    [CLB Cầu Lông Đống Đa]
   - Vai trò: HOST            - Vai trò: THÀNH VIÊN     - Vai trò: THỦ QUỸ
   - Quyền: 100% quyền hạn    - Quyền: Đăng ký, xem lịch- Quyền: Quản lý sổ quỹ
```

### 1.1. Các Nguyên Tắc Bất Biến:
1. **Tài khoản toàn cục (Global Identity)**: Người dùng đăng nhập chỉ tạo 1 tài khoản duy nhất `app_user` và nhận JWT định danh toàn cục (`sub` / `userId`).
2. **Quan hệ N-N độc lập (`group_member`)**:
   - Một người dùng có thể tham gia $N$ câu lạc bộ khác nhau.
   - Trong mỗi câu lạc bộ, người dùng có một bản ghi thành viên riêng với `RoleId` (`GroupRole`) và trạng thái (`ClubMemberStatus`) độc lập.
   - Trạng thái `BLOCKED` ở CLB A không làm ảnh hưởng đến quyền của người dùng ở CLB B.
3. **Quyền hạn gắn liền với ngữ cảnh nhóm (Contextual Permissions)**:
   - Cùng một người dùng thực hiện một hành động (ví dụ: tạo buổi chơi `SessionCreate`):
     * Nếu thực hiện trên **CLB Hà Đông** (nơi họ là Host) ➔ **Được phép (200 OK)**.
     * Nếu thực hiện trên **CLB Ba Đình** (nơi họ chỉ là Thành viên) ➔ **Bị từ chối (403 Forbidden)**.

---

## 2. Cơ Chế Thẩm Tra Quyền Động (Dynamic Permission Evaluation)

Tầng Presentation sử dụng `GroupPermissionHandler` để thẩm tra quyền dựa trên **cặp định danh (UserId, GroupId)** của từng Request cụ thể:

```
Client Request ──► [Endpoint .RequireGroupPermission(code)]
                          │
                          ▼
            [GroupPermissionHandler.HandleRequirementAsync]
                          │
     ┌────────────────────┴────────────────────┐
     ▼                                         ▼
1. Lấy UserId từ JWT Claims          2. Lấy GroupId từ RouteValues
   (sub / NameIdentifier)               ({id}, {groupId}, {clubId}) hoặc X-Group-Id
     │                                         │
     └────────────────────┬────────────────────┘
                          ▼
        [3. Kiểm tra IGroupAuthorizationService]
                          │
           ┌──────────────┴──────────────┐
           ▼                             ▼
   [Đã có trong Redis Cache?]      [Chưa có trong Cache]
   Key: group:perm:{userId}:{groupId}      Query BadmintonDbContext.GroupMembers
           │                                 Include(RoleEntity.RolePermissions)
           │                                 Lưu Redis Cache (TTL: 10 phút)
           ▼                                     │
   [Kiểm tra chứa permission code?] ◄────────────┘
           │
     ┌─────┴─────┐
     ▼           ▼
   [True]     [False]
Succeed(req) 403 Forbidden
```

### 2.1. Fast-Path Context Switching (Tùy chọn):
- Khi ứng dụng Mobile chuyển không gian làm việc chuyên sâu vào một CLB, Client có thể gọi `POST /api/v1/clubs/{id}/switch-context`.
- Endpoint này cấp JWT Token tạm thời chứa claim `tenant_id = {groupId}` và danh sách `permissions` của nhóm đó.
- `GroupPermissionHandler` hỗ trợ kiểm tra Fast-path: Nếu Token có `tenant_id == request.groupId` thì duyệt quyền ngay lập tức mà không cần truy vấn cache/DB.

---

## 3. Quy Chuẩn Truy Vấn & Lọc Dữ Liệu Tầng Persistence

> [!IMPORTANT]
> **CẤM cài đặt Global Query Filter ép cứng `tenant_id` trên toàn DbContext**:
> Vì người dùng cần:
> 1. Xem danh sách tất cả các CLB mình đang tham gia (`GET /api/v1/users/me/clubs`).
> 2. Tìm kiếm và xem thông tin các CLB công khai để xin gia nhập (`GET /api/v1/clubs`).
> 3. Xem lịch sử thi đấu và thống kê cá nhân xuyên suốt mọi CLB.

### 3.1. Quy tắc lọc dữ liệu theo ngữ cảnh:
- **Tài nguyên thuộc phạm vi 1 CLB cụ thể** (Buổi chơi `BadmintonSession`, Sổ quỹ `GroupLedger`, Lịch sinh hoạt `GroupSchedule`, Danh sách thành viên `GroupMember`):
  * Handler hoặc Repository **bắt buộc** phải có điều kiện lọc tường minh theo `GroupId`:
    ```csharp
    var sessions = await sessionRepository.FindListAsync(
        s => s.GroupId == request.GroupId && s.Status == SessionStatus.OPEN,
        cancellationToken);
    ```
- **Tài nguyên cá nhân của người dùng** (Lịch sử đặt slot `SessionBooking`, Thông báo `Notification`, Điểm Elo):
  * Truy vấn theo `userId`:
    ```csharp
    var bookings = await bookingRepository.FindListAsync(
        b => b.UserId == currentUserId,
        cancellationToken);
    ```

---

## 4. Danh Mục 18 Quyền Hạn Chuẩn (`GroupPermissions`)

Tất cả quyền hạn nhóm phải sử dụng hằng số từ `LegendsTeamVN.BadmintonClub.Domain.Constants.GroupPermissions`:

### 4.1. Phân hệ Vận hành sân (Operations - 8 quyền):
- `GroupPermissions.Operations.ScheduleManage`: Quản lý khung giờ sinh hoạt cố định.
- `GroupPermissions.Operations.SessionView`: Xem danh sách các buổi sinh hoạt.
- `GroupPermissions.Operations.SessionCreate`: Tạo mới buổi sinh hoạt cầu lông.
- `GroupPermissions.Operations.SessionUpdate`: Cập nhật thông tin buổi sinh hoạt.
- `GroupPermissions.Operations.SessionDelete`: Hủy hoặc xóa buổi sinh hoạt.
- `GroupPermissions.Operations.BookingManage`: Quản lý danh sách đăng ký tham gia.
- `GroupPermissions.Operations.MatchManage`: Quản lý trận đấu, điểm danh, xoay tua sân.
- `GroupPermissions.Operations.RecruitManage`: Quản lý đăng tin tuyển thêm khách vãng lai.

### 4.2. Phân hệ Tài chính & Sổ quỹ (Finance - 5 quyền):
- `GroupPermissions.Finance.LedgerView`: Xem sổ quỹ thu - chi của nhóm.
- `GroupPermissions.Finance.LedgerManage`: Ghi nhận khoản thu/chi thủ công vào sổ quỹ.
- `GroupPermissions.Finance.FeeView`: Xem cấu hình quy tắc thu phí.
- `GroupPermissions.Finance.FeeManage`: Điều chỉnh biểu phí hội viên, vãng lai, tiền cầu.
- `GroupPermissions.Finance.BankManage`: Cấu hình tài khoản ngân hàng nhận chuyển khoản VietQR.

### 4.3. Phân hệ Quản trị thành viên (Member - 5 quyền):
- `GroupPermissions.Member.View`: Xem danh sách thành viên trong nhóm.
- `GroupPermissions.Member.JoinRequestManage`: Duyệt hoặc từ chối yêu cầu xin gia nhập.
- `GroupPermissions.Member.RoleManage`: Phân quyền, tạo vai trò tùy chỉnh và gán vai trò.
- `GroupPermissions.Member.Remove`: Mời thành viên rời khỏi nhóm.
- `GroupPermissions.Member.InfoUpdate`: Cập nhật thông tin, biệt danh thành viên trong nhóm.

---

## 5. Danh Mục Kiểm Tra An Toàn (Checklist)

- [ ] Endpoint thao tác trên tài nguyên nhóm luôn có route parameter `{id:guid}` hoặc `{groupId:guid}` hoặc `{clubId:guid}`.
- [ ] Endpoint được gắn `.RequireGroupPermission(GroupPermissions.X)`.
- [ ] Handler nạp dữ liệu luôn kiểm tra sự tồn tại của tài nguyên thuộc đúng `GroupId`.
- [ ] Khi thay đổi vai trò hoặc quyền của thành viên trong nhóm, bắt buộc gọi:
  `groupAuthService.InvalidateUserPermissionsCacheAsync(userId, groupId)`.
