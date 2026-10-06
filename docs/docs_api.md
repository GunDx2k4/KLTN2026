# 📘 Tài Liệu Kỹ Thuật API - LegendsTeamVN BadmintonClub

Tài liệu chi tiết về tất cả các API đang hoạt động trên hệ thống Backend (`/api/v1`), bao gồm phương thức HTTP, endpoint, quyền truy cập (Permissions), tham số truyền vào và cấu trúc Response trả về.

---

## 📌 1. Quy Chuẩn Response & Lỗi (Standard Response Conventions)

Hệ thống sử dụng **Result Pattern** và **Custom ProblemDetails Standard**:

### 🟢 1.1. Thành công (Success Response)
* **200 OK**: Trả về Object JSON hoặc `PagedResult<T>` khi truy vấn dữ liệu thành công.
* **201 Created**: Trả về khi tạo mới thành công tài nguyên (Response chứa ID hoặc đường dẫn tài nguyên).
* **204 No Content**: Trả về khi cập nhật (PUT) hoặc xóa (DELETE) thành công.

### 🔴 1.2. Thất bại / Lỗi (Error Response Structure)
Khi có lỗi xảy ra (Validation, Unauthorized, NotFound, Server Error), API trả về định dạng chuẩn:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Resource Not Found",
  "status": 404,
  "detail": "A user with specified Id was not found.",
  "errors": [
    {
      "code": "User.NotFound",
      "description": "User with specified Id was not found."
    }
  ]
}
```

### 📄 1.3. Cấu trúc Phân trang (`PagedResult<T>`)
Tất cả các API lấy danh sách (`GET`) hỗ trợ phân trang sẽ trả về cấu trúc sau:

```json
{
  "items": [ ... ],
  "totalCount": 100,
  "pageNumber": 1,
  "pageSize": 10,
  "totalPages": 10,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

---

## 🔐 2. Phân Hệ Xác Thực & Tài Khoản (`/api/v1/auth`)

### 2.1. Đăng ký tài khoản (`POST /api/v1/auth/register`)
* **Mô tả**: Tạo tài khoản người dùng mới.
* **Xác thực**: Không yêu cầu (`Public`).
* **Request Body**:
  ```json
  {
    "email": "user@example.com",
    "password": "Password123@",
    "confirmPassword": "Password123@"
  }
  ```
* **Response Status**: `200 OK`
* **Response Body**:
  ```json
  {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "message": "User created successfully."
  }
  ```

---

### 2.2. Đăng nhập (`POST /api/v1/auth/login`)
* **Mô tả**: Đăng nhập vào hệ thống, trả về JWT Access Token và thiết lập Refresh Token trong HTTP-Only Cookie.
* **Xác thực**: Không yêu cầu (`Public`).
* **Request Body**:
  ```json
  {
    "email": "admin", 
    "password": "admin"
  }
  ```
  *(Trường `email` hỗ trợ cả Email chuẩn hoặc Username `admin`).*
* **Response Status**: `200 OK`
* **Response Body**:
  ```json
  {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  }
  ```
* **Header Response**: Gửi kèm Cookie `refreshToken` (HttpOnly, Secure, SameSite=Strict).

---

### 2.3. Làm mới Access Token (`POST /api/v1/auth/refresh`)
* **Mô tả**: Sử dụng Refresh Token từ Cookie để cấp lại Access Token mới.
* **Xác thực**: Gửi kèm `Authorization: Bearer <accessToken_cũ>` và Cookie `refreshToken`.
* **Response Status**: `200 OK`
* **Response Body**:
  ```json
  {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  }
  ```

---

### 2.4. Đăng xuất (`POST /api/v1/auth/logout`)
* **Mô tả**: Đăng xuất khỏi hệ thống, hủy phiên làm việc và xóa Cookie Refresh Token.
* **Xác thực**: Yêu cầu Bearer Token (`RequireAuthorization`).
* **Response Status**: `200 OK`
* **Response Body**:
  ```json
  {
    "message": "Logged out successfully."
  }
  ```

---

## 🛡️ 3. Phân Hệ Quản Lý Vai Trò / Nhóm Người Dùng (`/api/v1/roles`)

### 3.1. Xem danh sách vai trò (`GET /api/v1/roles`)
* **Mô tả**: Lấy danh sách tất cả các vai trò trong hệ thống kèm cây quyền tương ứng.
* **Xác thực**: Quyền **`Roles.Read`**.
* **Response Status**: `200 OK`
* **Response Body**:
  ```json
  [
    {
      "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "name": "Admin",
      "description": "Administrator role with full access",
      "permissions": [
        {
          "name": "System",
          "displayName": "Quản trị hệ thống",
          "children": [
            {
              "name": "Roles",
              "displayName": "Nhóm người dùng",
              "permissions": [
                { "name": "Roles.Read", "displayName": "Xem danh sách vai trò" },
                { "name": "Roles.Create", "displayName": "Tạo vai trò mới" },
                { "name": "Roles.Update", "displayName": "Chỉnh sửa vai trò" },
                { "name": "Roles.Delete", "displayName": "Xóa vai trò" },
                { "name": "Roles.AssignPermissions", "displayName": "Gán quyền cho vai trò" }
              ]
            }
          ]
        }
      ]
    }
  ]
  ```

---

### 3.2. Tạo vai trò mới (`POST /api/v1/roles`)
* **Mô tả**: Tạo một vai trò mới trong hệ thống.
* **Xác thực**: Quyền **`Roles.Create`**.
* **Request Body**:
  ```json
  {
    "name": "Staff",
    "description": "Nhân viên quản lý sân"
  }
  ```
* **Response Status**: `200 OK`

---

### 3.3. Chỉnh sửa vai trò (`PUT /api/v1/roles/{id}`)
* **Mô tả**: Cập nhật tên và mô tả của một vai trò theo ID.
* **Xác thực**: Quyền **`Roles.Update`**.
* **Path Parameter**: `id` (Guid).
* **Request Body**:
  ```json
  {
    "name": "Staff Senior",
    "description": "Nhân viên quản lý sân cao cấp"
  }
  ```
* **Response Status**: `204 No Content`

---

### 3.4. Xóa vai trò (`DELETE /api/v1/roles/{id}`)
* **Mô tả**: Xóa một vai trò khỏi hệ thống.
* **Xác thực**: Quyền **`Roles.Delete`**.
* **Path Parameter**: `id` (Guid).
* **Response Status**: `204 No Content`

---

### 3.5. Gán quyền cho vai trò (`PUT /api/v1/roles/{id}/permissions`)
* **Mô tả**: Gán/cập nhật danh sách quyền cho vai trò trong bảng `AppRolePermissions`.
* **Xác thực**: Quyền **`Roles.AssignPermissions`**.
* **Path Parameter**: `id` (Guid).
* **Request Body**:
  ```json
  {
    "permissions": [
      "Roles.Read",
      "Users.Read",
      "Venues.Read",
      "Courts.Read"
    ]
  }
  ```
* **Response Status**: `204 No Content`

---

## 👥 4. Phân Hệ Quản Lý Tài Khoản (`/api/v1/user`)

### 4.1. Lấy thông tin cá nhân người dùng (`GET /api/v1/user/me`)
* **Mô tả**: Trả về chi tiết cá nhân, danh sách vai trò và cây phân quyền của user đang đăng nhập.
* **Xác thực**: Bearer Token (`RequireAuthorization`).
* **Response Status**: `200 OK`
* **Response Body**:
  ```json
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "admin@admin.com",
    "userName": "admin",
    "phoneNumber": null,
    "isLocked": false,
    "lockoutEnd": null,
    "roles": ["Admin"],
    "permissions": [ ... ]
  }
  ```

---

### 4.2. Lấy danh sách quyền người dùng đang đăng nhập (`GET /api/v1/user/permissions`)
* **Mô tả**: Trả về vai trò và Cây phân quyền sở hữu bởi người dùng hiện tại.
* **Xác thực**: Bearer Token (`RequireAuthorization`).
* **Response Status**: `200 OK`
* **Response Body**:
  ```json
  {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "admin@admin.com",
    "roles": ["Admin"],
    "permissions": [
      {
        "name": "System",
        "displayName": "Quản trị hệ thống",
        "children": [
          {
            "name": "Roles",
            "displayName": "Nhóm người dùng",
            "permissions": [
              { "name": "Roles.Read", "displayName": "Xem danh sách vai trò" },
              { "name": "Roles.Create", "displayName": "Tạo vai trò mới" }
            ]
          }
        ]
      }
    ]
  }
  ```

---

### 4.3. Lấy toàn bộ định nghĩa quyền hệ thống (`GET /api/v1/user/all-permissions`)
* **Mô tả**: Trả về tất cả danh mục quyền trong hệ thống dưới dạng cây đa cấp.
* **Xác thực**: Quyền **`Users.Read`**.
* **Response Status**: `200 OK`

---

### 4.4. Lấy danh sách tài khoản phân trang (`GET /api/v1/user`)
* **Mô tả**: Truy vấn danh sách tài khoản toàn hệ thống.
* **Xác thực**: Quyền **`Users.Read`**.
* **Query Parameters**:
  * `pageNumber` (int, default: 1)
  * `pageSize` (int, default: 10)
  * `searchTerm` (string, optional)
* **Response Status**: `200 OK` (Trả về `PagedResult<UserResponse>`)

---

### 4.5. Lấy chi tiết tài khoản theo ID (`GET /api/v1/user/{id}`)
* **Mô tả**: Lấy thông tin tài khoản theo ID.
* **Xác thực**: Quyền **`Users.Read`**.
* **Path Parameter**: `id` (Guid).
* **Response Status**: `200 OK` / `404 Not Found`

---

### 4.6. Tạo tài khoản mới (`POST /api/v1/user`)
* **Mô tả**: Thêm tài khoản mới và gán vai trò ban đầu.
* **Xác thực**: Quyền **`Users.Create`**.
* **Request Body**:
  ```json
  {
    "email": "newuser@gmail.com",
    "password": "User123@",
    "roles": ["Manager"]
  }
  ```
* **Response Status**: `201 Created`

---

### 4.7. Chỉnh sửa thông tin tài khoản (`PUT /api/v1/user/{id}`)
* **Mô tả**: Cập nhật Email, Username, SĐT của tài khoản.
* **Xác thực**: Quyền **`Users.Update`**.
* **Path Parameter**: `id` (Guid).
* **Request Body**:
  ```json
  {
    "email": "updateduser@gmail.com",
    "userName": "updateduser",
    "phoneNumber": "0912345678"
  }
  ```
* **Response Status**: `204 No Content`

---

### 4.8. Xóa tài khoản (`DELETE /api/v1/user/{id}`)
* **Mô tả**: Xóa tài khoản người dùng khỏi hệ thống.
* **Xác thực**: Quyền **`Users.Delete`**.
* **Path Parameter**: `id` (Guid).
* **Response Status**: `204 No Content`

---

### 4.9. Khóa / Mở khóa tài khoản (`PUT /api/v1/user/{id}/lock`)
* **Mô tả**: Thay đổi trạng thái khóa/mở khóa tài khoản.
* **Xác thực**: Quyền **`Users.Lock`**.
* **Path Parameter**: `id` (Guid).
* **Request Body**:
  ```json
  {
    "isLocked": true
  }
  ```
* **Response Status**: `204 No Content`

---

### 4.10. Gán vai trò cho tài khoản (`PUT /api/v1/user/{id}/roles`)
* **Mô tả**: Cập nhật danh sách vai trò gán cho tài khoản.
* **Xác thực**: Quyền **`Users.AssignRoles`**.
* **Path Parameter**: `id` (Guid).
* **Request Body**:
  ```json
  {
    "roles": ["Admin", "Manager"]
  }
  ```
* **Response Status**: `204 No Content`

---

### 4.11. Đặt lại mật khẩu tài khoản (`POST /api/v1/user/{id}/reset-password`)
* **Mô tả**: Đặt lại mật khẩu mới cho tài khoản người dùng.
* **Xác thực**: Quyền **`Users.ResetPassword`**.
* **Path Parameter**: `id` (Guid).
* **Request Body**:
  ```json
  {
    "newPassword": "NewSecretPassword123@"
  }
  ```
* **Response Status**: `204 No Content`

---

## 🏢 5. Phân Hệ Quản Lý Cụm Sân (`/api/v1/venues`)

### 5.1. Lấy danh sách cụm sân (`GET /api/v1/venues`)
* **Mô tả**: Truy vấn danh sách cụm sân phân trang.
* **Xác thực**: Quyền **`Venues.Read`**.
* **Query Parameters**: `pageNumber`, `pageSize`, `searchTerm`, `isActive`.
* **Response Status**: `200 OK` (Trả về `PagedResult<VenueResponse>`)

---

### 5.2. Lấy chi tiết cụm sân (`GET /api/v1/venues/{id}`)
* **Mô tả**: Lấy thông tin cụm sân theo ID.
* **Xác thực**: Quyền **`Venues.Read`**.
* **Path Parameter**: `id` (Guid).
* **Response Status**: `200 OK` / `404 Not Found`

---

### 5.3. Tạo cụm sân mới (`POST /api/v1/venues`)
* **Mô tả**: Thêm mới một cụm sân cầu lông.
* **Xác thực**: Quyền **`Venues.Create`**.
* **Request Body**:
  ```json
  {
    "name": "Sân Cầu Lông Legends Bình Thạnh",
    "description": "Sân chuẩn quốc tế thảm Yonex",
    "address": "123 Điện Biên Phủ, Bình Thạnh, TP.HCM",
    "latitude": 10.8012,
    "longitude": 106.7112,
    "openTime": "06:00:00",
    "closeTime": "22:00:00"
  }
  ```
* **Response Status**: `201 Created`

---

### 5.4. Cập nhật cụm sân (`PUT /api/v1/venues/{id}`)
* **Mô tả**: Cập nhật thông tin cụm sân.
* **Xác thực**: Quyền **`Venues.Update`**.
* **Response Status**: `204 No Content`

---

### 5.5. Xóa cụm sân (`DELETE /api/v1/venues/{id}`)
* **Mô tả**: Xóa cụm sân khỏi hệ thống.
* **Xác thực**: Quyền **`Venues.Delete`**.
* **Response Status**: `204 No Content`

---

## 📅 6. Phân Hệ Lịch Hoạt Động Cụm Sân (`/api/v1/venue-schedules`)

### 6.1. Lấy danh sách lịch mở cửa (`GET /api/v1/venue-schedules`)
* **Xác thực**: Quyền **`VenueSchedules.Read`**.
* **Query Parameters**: `venueId`, `pageNumber`, `pageSize`.
* **Response Status**: `200 OK`

---

### 6.2. Tạo lịch mở cửa mới (`POST /api/v1/venue-schedules`)
* **Xác thực**: Quyền **`VenueSchedules.Create`**.
* **Request Body**:
  ```json
  {
    "venueId": "e5b88237-7729-455b-8012-9c123456789a",
    "dayOfWeek": 1,
    "openTime": "06:00:00",
    "closeTime": "22:00:00",
    "isClosed": false
  }
  ```
* **Response Status**: `201 Created`

---

### 6.3. Cập nhật lịch mở cửa (`PUT /api/v1/venue-schedules/{id}`)
* **Xác thực**: Quyền **`VenueSchedules.Update`**.
* **Response Status**: `204 No Content`

---

### 6.4. Xóa lịch mở cửa (`DELETE /api/v1/venue-schedules/{id}`)
* **Xác thực**: Quyền **`VenueSchedules.Delete`**.
* **Response Status**: `204 No Content`

---

## 🏸 7. Phân Hệ Quản Lý Sân Cầu Lông (`/api/v1/courts`)

### 7.1. Lấy danh sách sân (`GET /api/v1/courts`)
* **Xác thực**: Quyền **`Courts.Read`**.
* **Query Parameters**: `venueId`, `isAvailable`, `pageNumber`, `pageSize`.
* **Response Status**: `200 OK`

---

### 7.2. Lấy chi tiết sân (`GET /api/v1/courts/{id}`)
* **Xác thực**: Quyền **`Courts.Read`**.
* **Response Status**: `200 OK` / `404 Not Found`

---

### 7.3. Tạo sân mới (`POST /api/v1/courts`)
* **Xác thực**: Quyền **`Courts.Create`**.
* **Request Body**:
  ```json
  {
    "venueId": "e5b88237-7729-455b-8012-9c123456789a",
    "name": "Sân số 1 (Thảm VIP)",
    "description": "Sân thảm thi đấu Yonex",
    "pricePerHour": 100000
  }
  ```
* **Response Status**: `201 Created`

---

### 7.4. Cập nhật sân (`PUT /api/v1/courts/{id}`)
* **Xác thực**: Quyền **`Courts.Update`**.
* **Response Status**: `204 No Content`

---

### 7.5. Xóa sân (`DELETE /api/v1/courts/{id}`)
* **Xác thực**: Quyền **`Courts.Delete`**.
* **Response Status**: `204 No Content`

---

## 💰 8. Phân Hệ Bảng Giá Sân Theo Khung Giờ (`/api/v1/court-pricings`)

### 8.1. Lấy danh sách bảng giá (`GET /api/v1/court-pricings`)
* **Xác thực**: Quyền **`CourtPricings.Read`**.
* **Query Parameters**: `venueId`, `pageNumber`, `pageSize`.
* **Response Status**: `200 OK`

---

### 8.2. Tạo bảng giá mới (`POST /api/v1/court-pricings`)
* **Xác thực**: Quyền **`CourtPricings.Create`**.
* **Request Body**:
  ```json
  {
    "venueId": "e5b88237-7729-455b-8012-9c123456789a",
    "startTime": "17:00:00",
    "endTime": "21:00:00",
    "pricePerHour": 150000,
    "dayOfWeek": 6,
    "isPeakHour": true
  }
  ```
* **Response Status**: `201 Created`

---

### 8.3. Cập nhật bảng giá (`PUT /api/v1/court-pricings/{id}`)
* **Xác thực**: Quyền **`CourtPricings.Update`**.
* **Response Status**: `204 No Content`

---

### 8.4. Xóa bảng giá (`DELETE /api/v1/court-pricings/{id}`)
* **Xác thực**: Quyền **`CourtPricings.Delete`**.
* **Response Status**: `204 No Content`
