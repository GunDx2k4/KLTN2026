---
trigger: glob
globs: "src/LegendsTeamVN.BadmintonClub.Presentation/**, src/LegendsTeamVN.BadmintonClub.Application/**"
description: "Kiem soat phan quyen da nhom dong (Dynamic Contextual Multi-Group RBAC), GroupPermissions, Redis cache quyen han, va endpoint route guards."
---

# Quy Tac Phan Quyen Da Nhom Dong (Dynamic Contextual Multi-Group RBAC)

> [!IMPORTANT]
> He thong cau long phong trao hoat dong theo mo hinh **Da Nhom Linh Hoat (Multi-Club Ecosystem)**: 1 User co the tham gia N Cau lac bo voi N vai tro va quyen han hoan toan khac nhau. AI Agent tuyet doi khong ap dung mo hinh Single-Tenant hoac hard-code quyen han.

---

## 1. Ban Chat Nghiep Vu Cot Loi: 1 User — Nhieu Nhom — Nhieu Vai Tro

```
                          ┌────────────────────────┐
                          │   AppUser (Global)     │
                          │  (UserId: 8a4c-...-f1)  │
                          └───────────┬────────────┘
                                      │
            ┌─────────────────────────┼─────────────────────────┐
            ▼                         ▼                         ▼
   [CLB Cau Long Ha Dong]     [CLB Cau Long Ba Dinh]    [CLB Cau Long Dong Da]
   - Vai tro: HOST            - Vai tro: THANH VIEN     - Vai tro: THU QUY
   - Quyen: 100% quyen han    - Quyen: Dang ky, xem lich- Quyen: Quan ly so quy
```

### 1.1. Cac Nguyen Tac Bat Bien:
1. **Tai khoan toan cuc (Global Identity):** Nguoi dung dang nhap chi tao 1 tai khoan duy nhat `app_user` va nhan JWT dinh danh toan cuc (`sub` / `userId`).
2. **Quan he N-N doc lap (`group_member`):**
   - Mot nguoi dung co the tham gia N cau lac bo khac nhau.
   - Trong moi cau lac bo, nguoi dung co mot ban ghi thanh vien rieng voi `RoleId` (`GroupRole`) va trang thai (`ClubMemberStatus`) doc lap.
   - Trang thai `BLOCKED` o CLB A khong lam anh huong den quyen cua nguoi dung o CLB B.
3. **Quyen han gan lien voi ngu canh nhom (Contextual Permissions):**
   - Cung mot nguoi dung thuc hien mot hanh dong (vi du: tao buoi choi `SessionCreate`):
     - Neu thuc hien tren **CLB Ha Dong** (noi ho la Host) -> **Duoc phep (200 OK)**.
     - Neu thuc hien tren **CLB Ba Dinh** (noi ho chi la Thanh vien) -> **Bi tu choi (403 Forbidden)**.

---

## 2. Co Che Tham Tra Quyen Dong (Dynamic Permission Evaluation)

Tang Presentation su dung `GroupPermissionHandler` de tham tra quyen dua tren **cap dinh danh (UserId, GroupId)** cua tung Request cu the:

```
Client Request ──► [Endpoint .RequireGroupPermission(code)]
                          │
                          ▼
            [GroupPermissionHandler.HandleRequirementAsync]
                          │
     ┌────────────────────┴────────────────────┐
     ▼                                         ▼
1. Lay UserId tu JWT Claims          2. Lay GroupId tu RouteValues
   (sub / NameIdentifier)               ({id}, {groupId}, {clubId}) hoac X-Group-Id
     │                                         │
     └────────────────────┬────────────────────┘
                          ▼
        [3. Kiem tra IGroupAuthorizationService]
                          │
           ┌──────────────┴──────────────┐
           ▼                             ▼
   [Da co trong Redis Cache?]      [Chua co trong Cache]
   Key: group:perm:{userId}:{groupId}      Query BadmintonDbContext.GroupMembers
           │                                 Include(RoleEntity.RolePermissions)
           │                                 Luu Redis Cache (TTL: 10 phut)
           ▼                                     │
   [Kiem tra chua permission code?] ◄────────────┘
           │
     ┌─────┴─────┐
     ▼           ▼
   [True]     [False]
Succeed(req) 403 Forbidden
```

### 2.1. Cu phap bao ve Endpoint bat buoc:
```csharp
group.MapPost("{id:guid}/sessions", CreateSession)
    .WithName("CreateSession")
    .WithSummary("Tao buoi sinh hoat moi cho cau lac bo")
    .RequireGroupPermission(GroupPermissions.Operations.SessionCreate);
```

- **CAM hard-code chuoi quyen** (vi du: cam dung `"SessionCreate"` hay `"session.create"`). Bat buoc phai su dung hang so tu `LegendsTeamVN.BadmintonClub.Domain.Constants.GroupPermissions`.

---

## 3. Quy Chuan Truy Van & Loc Du Lieu Tang Persistence

> [!IMPORTANT]
> **CAM cai dat Global Query Filter ep cung `tenant_id` tren toan DbContext**:
> Vi nguoi dung can:
> 1. Xem danh sach tat ca cac CLB minh dang tham gia (`GET /api/v1/users/me/clubs`).
> 2. Tim kiem va xem thong tin cac CLB cong khai de xin gia nhap (`GET /api/v1/clubs`).
> 3. Xem lich su thi dau va thong ke ca nhan xuyen suot moi CLB.

### 3.1. Quy tac loc du lieu theo ngu canh:
- **Tai nguyen thuoc pham vi 1 CLB cu the** (Buoi choi `BadmintonSession`, So quy `GroupLedgerEntry`, Lich sinh hoat `GroupSchedule`, Danh sach thanh vien `GroupMember`):
  - Handler hoac Repository **bat buoc** phai co dieu kien loc tuong minh theo `GroupId`:
    ```csharp
    var sessions = await sessionRepository.FindListAsync(
        s => s.GroupId == request.GroupId && s.Status == SessionStatus.OPEN,
        cancellationToken);
    ```
- **Tai nguyen ca nhan cua nguoi dung** (Lich su dat slot `SessionBooking`, Thong bao `Notification`, Diem Elo):
  - Truy van theo `userId`:
    ```csharp
    var bookings = await bookingRepository.FindListAsync(
        b => b.UserId == currentUserId,
        cancellationToken);
    ```

---

## 4. Danh Muc 18 Quyen Han Chuan (`GroupPermissions`)

Tat ca quyen han nhom phai su dung hang so tu `LegendsTeamVN.BadmintonClub.Domain.Constants.GroupPermissions`:

### 4.1. Phan he Van hanh san (Operations - 8 quyen):
- `GroupPermissions.Operations.ScheduleManage`: Quan ly khung gio sinh hoat co dinh.
- `GroupPermissions.Operations.SessionView`: Xem danh sach cac buoi sinh hoat.
- `GroupPermissions.Operations.SessionCreate`: Tao moi buoi sinh hoat cau long.
- `GroupPermissions.Operations.SessionUpdate`: Cap nhat thong tin buoi sinh hoat.
- `GroupPermissions.Operations.SessionDelete`: Huy hoac xoa buoi sinh hoat.
- `GroupPermissions.Operations.BookingManage`: Quan ly danh sach dang ky tham gia.
- `GroupPermissions.Operations.MatchManage`: Quan ly tran dau, diem danh, xoay tua san.
- `GroupPermissions.Operations.RecruitManage`: Quan ly dang tin tuyen them khach vang lai.

### 4.2. Phan he Tai chinh & So quy (Finance - 5 quyen):
- `GroupPermissions.Finance.LedgerView`: Xem so quy thu - chi cua nhom.
- `GroupPermissions.Finance.LedgerManage`: Ghi nhan khoan thu/chi thu cong vao so quy.
- `GroupPermissions.Finance.FeeView`: Xem cau hinh quy tac thu phi.
- `GroupPermissions.Finance.FeeManage`: Dieu chinh bieu phi hoi vien, vang lai, tien cau.
- `GroupPermissions.Finance.BankManage`: Cau hinh tai khoan ngan hang nhan chuyen khoan VietQR.

### 4.3. Phan he Quan tri thanh vien (Member - 5 quyen):
- `GroupPermissions.Member.View`: Xem danh sach thanh vien trong nhom.
- `GroupPermissions.Member.JoinRequestManage`: Duyet hoac tu choi yeu cau xin gia nhap.
- `GroupPermissions.Member.RoleManage`: Phan quyen, tao vai tro tuy chinh va gan vai tro.
- `GroupPermissions.Member.Remove`: Moi thanh vien roi khoi nhom.
- `GroupPermissions.Member.InfoUpdate`: Cap nhat thong tin, biet danh thanh vien trong nhom.

---

## 5. Danh Muc Kiem Tra An Toan (Security Checklist)

- [ ] Endpoint thao tac tren tai nguyen nhom luon co route parameter `{id:guid}` hoac `{groupId:guid}` hoac `{clubId:guid}`.
- [ ] Endpoint duoc gan `.RequireGroupPermission(GroupPermissions.<Module>.<Permission>)`.
- [ ] Handler nap du lieu luon kiem tra su ton tai cua tai nguyen thuoc dung `GroupId`.
- [ ] Khi thay doi vai tro hoac quyen cua thanh vien trong nhom, bat buoc goi:
  `groupAuthService.InvalidateUserPermissionsCacheAsync(userId, groupId)`.
