# 📖 Tài Liệu Danh Sách API (API Specification & Reference)

Tài liệu chi tiết toàn bộ các đầu API (RESTful Minimal APIs) hiện có của hệ thống **LegendsTeamVN.BadmintonClub**.

---

## 📌 Thông Tin Chung

- **Base URL (Local Dev)**: `http://localhost:54796/api/v1`
- **Swagger UI**: [http://localhost:54796/swagger](http://localhost:54796/swagger)
- **Chuẩn xác thực (Authentication)**: `Authorization: Bearer <ACCESS_TOKEN>`
- **Phân quyền đa nhóm (Multi-Group RBAC)**:
  - Context câu lạc bộ được tự động trích xuất qua Route `{id:guid}` (hoặc `{clubId:guid}`) hoặc Header `X-Group-Id: <GUID>`.
  - Quyền hạn kiểm tra theo bảng mã `GroupPermissions` tại [GroupPermissions.cs](file:///c:/code/abp_be/KLTN2026/src/LegendsTeamVN.BadmintonClub.Domain/Constants/GroupPermissions.cs).
- **Chuẩn lỗi (RFC 7807 & Result Pattern)**:
  ```json
  {
    "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
    "title": "Bad Request",
    "status": 400,
    "detail": "Tên câu lạc bộ không được để trống.",
    "errors": {
      "Name": ["Tên câu lạc bộ không được để trống."]
    }
  }
  ```

---

## 📑 Mục Lục Các Module

1. [Module 1: Xác Thực & Đăng Nhập (Auth)](#1-module-xác-thực--đăng-nhập-auth) *(5 APIs)*
2. [Module 2: Quản Trị Người Dùng (Users)](#2-module-quản-trị-người-dùng-users) *(11 APIs)*
3. [Module 3: Vai Trò Hệ Thống Toàn Cục (System Roles)](#3-module-vai-trò-hệ-thống-toàn-cục-system-roles) *(5 APIs)*
4. [Module 4: Câu Lạc Bộ & Thành Viên (Clubs & Members)](#4-module-câu-lạc-bộ--thành-viên-clubs--members) *(8 APIs)*
5. [Module 5: Vai Trò Nội Bộ Nhóm (Group Roles)](#5-module-vai-trò-nội-bộ-nhóm-group-roles) *(4 APIs)*
6. [Module 6: Xét Duyệt Đơn Gia Nhập (Join Requests)](#6-module-xét-duyệt-đơn-gia-nhập-join-requests) *(4 APIs)*

---

## 1. Module Xác Thực & Đăng Nhập (Auth)
Prefix: `/api/v1/auth`

| Method | Endpoint | Yêu cầu Auth | Mô tả |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/auth/register` | Public | Đăng ký tài khoản người dùng mới |
| `POST` | `/api/v1/auth/external-register` | Public | Đăng ký / đăng nhập qua Google hoặc Apple |
| `POST` | `/api/v1/auth/login` | Public | Đăng nhập hệ thống (trả về JWT, set cookie refresh token) |
| `POST` | `/api/v1/auth/refresh` | Cookie `refreshToken` | Cấp mới Access Token bằng Refresh Token |
| `POST` | `/api/v1/auth/logout` | Bearer Token | Đăng xuất, hủy phiên và xóa cookie refresh token |

### Chi tiết API:

#### 1.1. `POST /api/v1/auth/register`
- **Request Body**:
  ```json
  {
    "email": "user@example.com",
    "userName": "nguyenvana",
    "password": "Password@123",
    "confirmPassword": "Password@123",
    "fullName": "Nguyễn Văn A",
    "phoneNumber": "0987654321"
  }
  ```
- **Response `201 Created`**:
  ```json
  {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "accessToken": "eyJhbGciOiJIUzI1NiIs...",
    "refreshToken": "d8f9e...",
    "refreshTokenExpiryTime": "2026-10-19T08:00:00Z"
  }
  ```

#### 1.2. `POST /api/v1/auth/login`
- **Request Body**:
  ```json
  {
    "emailOrUserName": "user@example.com",
    "password": "Password@123"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "accessToken": "eyJhbGciOiJIUzI1NiIs...",
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "userName": "nguyenvana",
    "email": "user@example.com",
    "roles": ["User"]
  }
  ```

#### 1.3. `POST /api/v1/auth/refresh`
- **Headers / Cookies**: Header `Authorization: Bearer <EXPIRED_TOKEN>`, Cookie `refreshToken=<TOKEN>`
- **Response `200 OK`**:
  ```json
  {
    "accessToken": "eyJhbGciOiJIUzI1NiIs..."
  }
  ```

#### 1.4. `POST /api/v1/auth/logout`
- **Headers**: `Authorization: Bearer <ACCESS_TOKEN>`
- **Response `200 OK`**: Thu hồi refreshToken thành công và xóa cookie.

---

## 2. Module Quản Trị Người Dùng (Users)
Prefix: `/api/v1/user`

| Method | Endpoint | Quyền hạn yêu cầu | Mô tả |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/user/me` | Bearer Token | Lấy thông tin tài khoản, roles & cây quyền của chính mình |
| `GET` | `/api/v1/user/permissions` | Bearer Token | Lấy danh sách cây quyền của tài khoản hiện tại |
| `GET` | `/api/v1/user/all-permissions` | `Users.Read` | Lấy toàn bộ cây danh mục quyền của hệ thống |
| `GET` | `/api/v1/user` | `Users.Read` | Lấy danh sách người dùng có phân trang và tìm kiếm |
| `GET` | `/api/v1/user/{id}` | `Users.Read` | Xem chi tiết người dùng theo ID |
| `POST` | `/api/v1/user` | `Users.Create` | Tạo mới tài khoản người dùng từ trang quản trị |
| `PUT` | `/api/v1/user/{id}` | `Users.Update` | Cập nhật thông tin email, username, phone |
| `DELETE` | `/api/v1/user/{id}` | `Users.Delete` | Xóa tài khoản người dùng |
| `PUT` | `/api/v1/user/{id}/lock` | `Users.Lock` | Khóa hoặc mở khóa tài khoản |
| `PUT` | `/api/v1/user/{id}/roles` | `Users.AssignRoles` | Gán danh sách vai trò hệ thống cho người dùng |
| `POST` | `/api/v1/user/{id}/reset-password` | `Users.ResetPassword` | Đặt lại mật khẩu tài khoản |

### Chi tiết API quan trọng:

#### 2.1. `GET /api/v1/user/me`
- **Headers**: `Authorization: Bearer <TOKEN>`
- **Response `200 OK`**:
  ```json
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "userName": "admin",
    "email": "admin@admin.com",
    "phoneNumber": "0901234567",
    "roles": ["Admin"],
    "permissions": [
      {
        "groupName": "Users",
        "permissions": ["Permissions.Users.Read", "Permissions.Users.Create"]
      }
    ]
  }
  ```

#### 2.2. `GET /api/v1/user`
- **Query Parameters**:
  - `pageIndex` (int, default: 1)
  - `pageSize` (int, default: 10)
  - `searchTerm` (string, optional)
- **Response `200 OK`**:
  ```json
  {
    "items": [
      {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "userName": "admin",
        "email": "admin@admin.com",
        "phoneNumber": "0901234567",
        "isLocked": false,
        "roles": ["Admin"]
      }
    ],
    "totalCount": 1,
    "pageIndex": 1,
    "pageSize": 10
  }
  ```

---

## 3. Module Vai Trò Hệ Thống Toàn Cục (System Roles)
Prefix: `/api/v1/roles`

> Quản lý vai trò toàn cục của ứng dụng (Admin, Manager, User) của Identity.

| Method | Endpoint | Quyền hạn yêu cầu | Mô tả |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/roles` | `Roles.Read` | Lấy danh sách toàn bộ vai trò hệ thống kèm quyền hạn |
| `POST` | `/api/v1/roles` | `Roles.Create` | Tạo mới một vai trò hệ thống |
| `PUT` | `/api/v1/roles/{id}` | `Roles.Update` | Đổi tên hoặc mô tả vai trò hệ thống |
| `DELETE` | `/api/v1/roles/{id}` | `Roles.Delete` | Xóa một vai trò hệ thống |
| `PUT` | `/api/v1/roles/{id}/permissions` | `Roles.AssignPermissions` | Gán danh sách quyền hệ thống cho vai trò |

---

## 4. Module Câu Lạc Bộ & Thành Viên (Clubs & Members)
Prefix: `/api/v1/clubs`

| Method | Endpoint | Quyền hạn yêu cầu | Mô tả |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/clubs` | Bearer Token | Tạo CLB mới (người tạo tự động làm Host full 100% quyền) |
| `GET` | `/api/v1/clubs/my-clubs` | Bearer Token | **FR03**: Lấy danh sách CLB user tham gia kèm vai trò độc lập theo từng nhóm |
| `GET` | `/api/v1/clubs/{id}` | Bearer Token | Xem thông tin chi tiết của một câu lạc bộ |
| `POST` | `/api/v1/clubs/join` | Bearer Token | Tham gia CLB trực tiếp bằng mã Code nhóm |
| `GET` | `/api/v1/clubs/{id}/members` | `MEMBER.VIEW` | Xem danh sách thành viên và vai trò trong CLB |
| `PUT` | `/api/v1/clubs/{id}/members/{userId}/role` | `MEMBER.ROLE.MANAGE` | Bổ nhiệm / cập nhật vai trò cho thành viên trong CLB |
| `DELETE` | `/api/v1/clubs/{id}/members/{userId}` | Bearer Token | Rời nhóm (nếu chính mình) hoặc kick thành viên (nếu là Host) |
| `POST` | `/api/v1/clubs/{id}/switch-context` | Bearer Token | **FR04**: Đổi context sang CLB này, cấp Token mới chứa đúng vai trò & quyền hạn |

### Chi tiết API:

#### 4.1. `POST /api/v1/clubs`
- **Request Body**:
  ```json
  {
    "name": "CLB Cầu Lông Legends Quận 7",
    "description": "Sinh hoạt tối thứ 3 - 5 - 7 hàng tuần sân Kim Cương",
    "avatarUrl": "https://example.com/avatar.png"
  }
  ```
- **Response `201 Created`**:
  ```json
  "b86fa2e7-03b6-4299-923c-a93108ce8968"
  ```

#### 4.2. `GET /api/v1/clubs/my-clubs`
- **Mô tả**: Hỗ trợ người dùng tham gia nhiều câu lạc bộ đồng thời, mỗi câu lạc bộ giữ một vai trò độc lập (Host, Thủ quỹ, Thành viên...).
- **Response `200 OK`**:
  ```json
  [
    {
      "groupId": "b86fa2e7-03b6-4299-923c-a93108ce8968",
      "groupName": "CLB Cầu Lông Legends Quận 7",
      "code": "CLB-A1B2C3",
      "description": "Sân Kim Cương",
      "avatarUrl": "https://example.com/avatar1.png",
      "roleId": "e1586a65-1d02-4fc5-9118-28308bc25752",
      "roleName": "Chủ phòng (Host)",
      "status": "ACTIVE",
      "joinedAt": "2026-10-12T07:00:00Z"
    },
    {
      "groupId": "92186981-d1c9-4a9f-8566-f002237885b1",
      "groupName": "Badminton Masters Thủ Đức",
      "code": "CLB-X9Y8Z7",
      "description": "Giao lưu cuối tuần",
      "avatarUrl": null,
      "roleId": "1b08ce89-03b6-4299-923c-a93108ce8968",
      "roleName": "Thủ quỹ",
      "status": "ACTIVE",
      "joinedAt": "2026-10-10T15:30:00Z"
    }
  ]
  ```

#### 4.3. `POST /api/v1/clubs/{id}/switch-context`
- **Mô tả**: Khi người dùng chuyển sang câu lạc bộ muốn thao tác, backend kiểm tra vai trò tại nhóm đó và cấp JWT Access Token mới chứa `tenantId: clubId` và toàn bộ mã quyền hạn thực tế.
- **Response `200 OK`**:
  ```json
  {
    "accessToken": "eyJhbGciOiJIUzI1NiIs...",
    "clubId": "b86fa2e7-03b6-4299-923c-a93108ce8968",
    "clubName": "CLB Cầu Lông Legends Quận 7",
    "roleId": "e1586a65-1d02-4fc5-9118-28308bc25752",
    "roleName": "Chủ phòng (Host)",
    "permissions": [
      "OPERATIONS.SESSION.VIEW",
      "OPERATIONS.SESSION.CREATE",
      "FINANCE.LEDGER.VIEW",
      "MEMBER.VIEW",
      "MEMBER.ROLE.MANAGE"
    ]
  }
  ```

#### 4.4. `PUT /api/v1/clubs/{id}/members/{userId}/role`
- **Request Body**:
  ```json
  {
    "roleId": "1b08ce89-03b6-4299-923c-a93108ce8968"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "success": true
  }
  ```

---

## 5. Module Vai Trò Nội Bộ Nhóm (Group Roles)
Prefix: `/api/v1/clubs/{id}/roles`

> Quản lý các vai trò tùy chỉnh và phân bổ quyền động bên trong từng câu lạc bộ cụ thể.

| Method | Endpoint | Quyền hạn yêu cầu | Mô tả |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/clubs/{id}/roles` | `MEMBER.VIEW` | Lấy danh sách toàn bộ vai trò của CLB kèm danh sách quyền hạn |
| `POST` | `/api/v1/clubs/{id}/roles` | `MEMBER.ROLE.MANAGE` | Tạo vai trò mới cho CLB (ví dụ: "Phó ban chuyên môn", "Thủ quỹ") |
| `PUT` | `/api/v1/clubs/{id}/roles/{roleId}/permissions` | `MEMBER.ROLE.MANAGE` | Cập nhật bộ quyền hạn cho vai trò trong CLB |
| `DELETE` | `/api/v1/clubs/{id}/roles/{roleId}` | `MEMBER.ROLE.MANAGE` | Xóa vai trò tùy chỉnh khỏi CLB |

### Chi tiết API:

#### 5.1. `GET /api/v1/clubs/{id}/roles`
- **Response `200 OK`**:
  ```json
  [
    {
      "id": "e1586a65-1d02-4fc5-9118-28308bc25752",
      "clubId": "b86fa2e7-03b6-4299-923c-a93108ce8968",
      "roleName": "Chủ phòng (Host)",
      "description": "Chủ sở hữu và quản trị viên tối cao",
      "isDefault": false,
      "isHostRole": true,
      "createdAt": "2026-10-12T07:00:00Z",
      "permissions": [
        {
          "id": "c1d1a1b1-1111-2222-3333-444455556666",
          "code": "OPERATIONS.SESSION.VIEW",
          "name": "Xem buổi sinh hoạt",
          "moduleGroup": "OPERATIONS"
        }
      ]
    }
  ]
  ```

#### 5.2. `POST /api/v1/clubs/{id}/roles`
- **Request Body**:
  ```json
  {
    "roleName": "Thủ quỹ",
    "description": "Quản lý sổ thu chi và đối soát hội phí buổi chơi",
    "permissionIds": [
      "c1d1a1b1-1111-2222-3333-444455556666",
      "d2e2b2c2-2222-3333-4444-555566667777"
    ],
    "isDefault": false
  }
  ```
- **Response `201 Created`**:
  ```json
  {
    "roleId": "4f9d3b1a-82c5-4011-b1e8-7a5f6e8c9d01"
  }
  ```

#### 5.3. `PUT /api/v1/clubs/{id}/roles/{roleId}/permissions`
- **Request Body**:
  ```json
  {
    "permissionIds": [
      "c1d1a1b1-1111-2222-3333-444455556666",
      "d2e2b2c2-2222-3333-4444-555566667777"
    ]
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "success": true
  }
  ```
  *(Hệ thống tự động xóa cache `group:perm:{userId}:{groupId}` của mọi thành viên đang giữ role này để quyền có hiệu lực tức thì)*.

#### 5.4. `DELETE /api/v1/clubs/{id}/roles/{roleId}`
- **Ràng buộc an toàn**:
  - Không thể xóa vai trò Host (vai trò sở hữu 100% quyền).
  - Không thể xóa vai trò mặc định (`isDefault = true`).
  - Không thể xóa vai trò đang được phân cho ít nhất một thành viên hoạt động.
- **Response `204 NoContent`**.

---

## 6. Module Xét Duyệt Đơn Gia Nhập (Join Requests)
Prefix: `/api/v1/clubs/{id}/join-requests`

| Method | Endpoint | Quyền hạn yêu cầu | Mô tả |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/v1/clubs/{id}/join-requests` | Bearer Token | Người dùng gửi đơn xin tham gia CLB |
| `GET` | `/api/v1/clubs/{id}/join-requests` | `MEMBER.JOIN_REQUEST.MANAGE` | Ban quản trị xem danh sách đơn đang chờ duyệt |
| `PUT` | `/api/v1/clubs/{id}/join-requests/{requestId}/approve` | `MEMBER.JOIN_REQUEST.MANAGE` | Duyệt đơn và kết nạp thành viên mới kèm vai trò |
| `PUT` | `/api/v1/clubs/{id}/join-requests/{requestId}/reject` | `MEMBER.JOIN_REQUEST.MANAGE` | Từ chối đơn xin gia nhập |

### Chi tiết API:

#### 6.1. `POST /api/v1/clubs/{id}/join-requests`
- **Request Body**:
  ```json
  {
    "introduction": "Chào Admin, mình trình độ Trung bình khá (TB_K), muốn tham gia sinh hoạt định kỳ tối thứ 3-5-7."
  }
  ```
- **Response `201 Created`**:
  ```json
  {
    "requestId": "7e3a2b1c-8844-4911-9a22-3c4d5e6f7a8b"
  }
  ```

#### 6.2. `GET /api/v1/clubs/{id}/join-requests`
- **Query Parameters**:
  - `status` (string, optional): `PENDING`, `APPROVED`, hoặc `REJECTED`. Để trống sẽ trả về tất cả.
- **Response `200 OK`**:
  ```json
  [
    {
      "id": "7e3a2b1c-8844-4911-9a22-3c4d5e6f7a8b",
      "clubId": "b86fa2e7-03b6-4299-923c-a93108ce8968",
      "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "fullName": "Trần Văn B",
      "email": "tranvanb@gmail.com",
      "phoneNumber": "0912345678",
      "avatarUrl": "https://example.com/avatar.jpg",
      "introduction": "Xin gia nhập nhóm...",
      "status": "PENDING",
      "createdAt": "2026-10-12T08:15:00Z",
      "reviewedAt": null,
      "approvedBy": null
    }
  ]
  ```

#### 6.3. `PUT /api/v1/clubs/{id}/join-requests/{requestId}/approve`
- **Request Body**:
  ```json
  {
    "roleId": "4f9d3b1a-82c5-4011-b1e8-7a5f6e8c9d01",
    "memberType": "MONTHLY"
  }
  ```
  *(Nếu `roleId` để `null`, hệ thống tự động gán vai trò mặc định của CLB)*.
- **Response `200 OK`**:
  ```json
  {
    "memberId": "8b9c0d1e-2f3a-4b5c-6d7e-8f9a0b1c2d3e"
  }
  ```

#### 6.4. `PUT /api/v1/clubs/{id}/join-requests/{requestId}/reject`
- **Response `200 OK`**:
  ```json
  {
    "success": true
  }
  ```

---

## 📌 Bảng Tra Cứu Toàn Bộ Mã Quyền Hạn (`GroupPermissions`)

Dùng để cấu hình vai trò nhóm hoặc gửi kèm kiểm tra quyền:

| Phân Nhóm | Mã Quyền (`Code`) | Tên Quyền |
| :--- | :--- | :--- |
| **OPERATIONS** | `OPERATIONS.SCHEDULE.MANAGE` | Quản lý lịch sinh hoạt cố định |
| **OPERATIONS** | `OPERATIONS.SESSION.VIEW` | Xem danh sách & chi tiết buổi sinh hoạt |
| **OPERATIONS** | `OPERATIONS.SESSION.CREATE` | Tạo buổi sinh hoạt mới |
| **OPERATIONS** | `OPERATIONS.SESSION.UPDATE` | Cập nhật buổi sinh hoạt |
| **OPERATIONS** | `OPERATIONS.SESSION.DELETE` | Hủy / xóa buổi sinh hoạt |
| **OPERATIONS** | `OPERATIONS.BOOKING.MANAGE` | Quản lý đặt chỗ & điểm danh |
| **OPERATIONS** | `OPERATIONS.MATCH.MANAGE` | Quản lý trận đấu & ghép sân |
| **OPERATIONS** | `OPERATIONS.RECRUIT.MANAGE` | Đăng tin tuyển khách vãng lai |
| **FINANCE** | `FINANCE.LEDGER.VIEW` | Xem sổ quỹ thu chi nhóm |
| **FINANCE** | `FINANCE.LEDGER.MANAGE` | Lập / sửa phiếu thu chi quỹ |
| **FINANCE** | `FINANCE.FEE.VIEW` | Xem danh sách thu hội phí |
| **FINANCE** | `FINANCE.FEE.MANAGE` | Thu phí & đối soát ngân hàng |
| **FINANCE** | `FINANCE.BANK.MANAGE` | Quản lý tài khoản ngân hàng & đơn giá |
| **MEMBER** | `MEMBER.VIEW` | Xem danh sách thành viên trong nhóm |
| **MEMBER** | `MEMBER.JOIN_REQUEST.MANAGE` | Duyệt / từ chối đơn xin gia nhập nhóm |
| **MEMBER** | `MEMBER.ROLE.MANAGE` | Quản lý vai trò & phân quyền nhóm |
| **MEMBER** | `MEMBER.REMOVE` | Xóa thành viên khỏi nhóm |
| **MEMBER** | `MEMBER.INFO.UPDATE` | Cập nhật thông tin nhóm (tên, avatar...) |
