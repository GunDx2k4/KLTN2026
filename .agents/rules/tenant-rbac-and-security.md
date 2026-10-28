---
trigger: "src/LegendsTeamVN.BadmintonClub.Presentation/**, src/LegendsTeamVN.BadmintonClub.Application/**"
description: "Kiểm soát phân quyền đa nhóm động (Dynamic Contextual Multi-Group RBAC), GroupPermissions, Redis cache quyền hạn, và endpoint route guards."
---

# Quy Tắc Phân Quyền Đa Nhóm Động (Dynamic Contextual Multi-Group RBAC)

Tài liệu định nghĩa các quy tắc bảo mật và phân quyền bắt buộc trong hệ sinh thái câu lạc bộ cầu lông: 1 Người dùng $\rightarrow$ N Nhóm $\rightarrow$ N Vai trò độc lập.

---

## 1. Bản Chất Nghiệp Vụ Cốt Lõi: 1 User — Nhiều Nhóm — Nhiều Vai Trò

```text
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

### Các nguyên tắc bất biến:
1. **Tài Khoản Toàn Cục (Global Identity):** Người dùng đăng nhập chỉ tạo 1 tài khoản duy nhất `app_user` và nhận JWT định danh toàn cục (`sub` / `userId`).
2. **Quan Hệ N-N Độc Lập (`group_member`):**
   - Một người dùng có thể tham gia N câu lạc bộ khác nhau.
   - Trong mỗi câu lạc bộ, người dùng có một bản ghi thành viên riêng với `RoleId` (`GroupRole`) và trạng thái (`ClubMemberStatus`) độc lập.
   - Trạng thái `BLOCKED` ở CLB A không làm ảnh hưởng đến quyền của người dùng ở CLB B.
3. **Quyền Hạn Gắn Liền Với Ngữ Cảnh Nhóm (Contextual Permissions):**
   - Cùng một người dùng thực hiện một hành động (ví dụ: tạo buổi chơi `SessionCreate`):
     - Nếu thực hiện trên **CLB Hà Đông** (nơi họ là Host) $\rightarrow$ **Được phép (200 OK)**.
     - Nếu thực hiện trên **CLB Ba Đình** (nơi họ chỉ là Thành viên) $\rightarrow$ **Bị từ chối (403 Forbidden)**.
4. **CẤM Global Query Filter Khóa `tenant_id`:** Tuyệt đối không dùng Global Query Filter EF Core để khóa cứng `tenant_id` toàn bộ request, vì người dùng tương tác xuyên nhiều CLB trong cùng một phiên làm việc.

---

## 2. Cơ Chế Thẩm Tra Quyền Động (Dynamic Permission Evaluation)

Tầng Presentation sử dụng `GroupPermissionHandler` để thẩm tra quyền dựa trên **cặp định danh (UserId, GroupId)** của từng Request cụ thể:

```text
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
    Có          Không
     ▼           ▼
[Succeed]   [403 Forbidden]
```

---

## 3. Quy Chuẩn Đặt Endpoint Guard (Endpoint Route Guards)

1. **Club-Scoped Endpoints Bắt Buộc Gắn Guard:**
   - Mọi route chứa `{id:guid}`, `{groupId:guid}`, hoặc `{clubId:guid}` liên quan đến tài nguyên CLB bắt buộc phải được bảo vệ bởi `.RequireGroupPermission(GroupPermissions.*)`.
2. **Cấm Hardcode Chuỗi Quyền:**
   - Sử dụng hằng số tĩnh từ class `GroupPermissions`.
   - Tuyệt đối cấm viết chuỗi tự do (như `"CLUBS.CREATE"`, `"HOST"`, `"ADMIN"`).
3. **Vô Hiệu Hóa Cache Khi Đổi Quyền (Cache Invalidation):**
   - Khi Host thay đổi vai trò của thành viên (`ChangeMemberRoleCommand`) hoặc cập nhật danh sách quyền của Role (`UpdateRolePermissionsCommand`), bắt buộc xóa cache Redis:
     `Key: group:perm:{userId}:{groupId}`.
