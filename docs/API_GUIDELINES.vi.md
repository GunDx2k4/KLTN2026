# 🌐 Hướng dẫn Thiết kế API (API Guidelines)

Để đảm bảo tính nhất quán và dễ dàng tích hợp cho bộ phận Frontend / Mobile App, tất cả các kỹ sư phần mềm khi phát triển tính năng mới tại dự án LegendsTeamVN phải tuân thủ nghiêm ngặt các quy tắc thiết kế API dưới đây.

*[Read this in English](API_GUIDELINES.md)*

---

## 1. Chuẩn RESTful API

Chúng tôi áp dụng mô hình thiết kế **Resource-oriented (Hướng tài nguyên)**. Tuyệt đối không nhúng động từ (Verbs) vào đường dẫn (URL) ngoại trừ các hành động quá đặc thù.

### Quy ước Đặt tên (Naming Conventions):
- Sử dụng danh từ số nhiều (Plural nouns) cho tất cả các endpoint.
- Viết thường toàn bộ và phân cách bằng dấu gạch ngang (`kebab-case`).

✅ **Nên làm (Chuẩn RESTful):**
- `GET /api/users` (Lấy danh sách người dùng)
- `GET /api/users/{id}` (Lấy chi tiết 1 người dùng)
- `POST /api/users` (Tạo mới người dùng)
- `PUT /api/users/{id}` (Cập nhật toàn bộ thông tin)
- `DELETE /api/users/{id}` (Xóa người dùng)

❌ **Tránh làm (Anti-patterns):**
- `GET /api/get-all-users`
- `POST /api/users/create`
- `POST /api/users/{id}/delete`

### Đối với các Hành động Đặc thù (Actions):
Khi hành động không thể mô tả bằng CRUD, hãy thêm động từ vào sau định danh tài nguyên.
✅ `POST /api/users/{id}/activate` (Kích hoạt tài khoản)

---

## 2. Chuẩn Hóa Dữ liệu Trả về (Uniform Response Format)

Dự án này **KHÔNG** bọc (wrap) thành công HTTP Responses trong một cấu trúc tùy chỉnh phức tạp kiểu `{ "isSuccess": true, "data": ... }`. Thay vào đó, chúng ta tin tưởng vào **HTTP Status Codes** gốc. Tuy nhiên, logic code nội bộ phải xử lý chuẩn xác bằng **Result Pattern**.

### Tại tầng Application (CQRS Handler):
Tất cả các Handlers bắt buộc phải trả về kiểu `Result<T>`. Bạn không bao giờ được `throw Exception` để kiểm soát luồng logic (Control flow) như việc không tìm thấy dữ liệu hay dữ liệu sai. 
Thay vào đó, hãy trả về lỗi tường minh:

```csharp
// Khi thành công
return Result.Success(userDto);

// Khi thất bại
return Result.Failure<UserResponse>(DomainErrors.User.NotFound);
```

### Tại tầng Presentation (Minimal API Endpoints):
Endpoint có nhiệm vụ "bóc vỏ" `Result<T>` và trả về đúng chuẩn HTTP thông qua hàm `.Match()` hoặc `.MapToIResult()`.

```csharp
private static async Task<IResult> GetUserById([AsParameters] GetUserRequest request, ISender sender)
{
    var result = await sender.Send(new GetUserQuery(request.Id));
    
    return result.Match(
        onSuccess: response => Results.Ok(response), // Trả về HTTP 200 kèm DTO
        onFailure: error => Results.BadRequest(error) // Hoặc được extension tự map ra ProblemDetails
    );
}
```

---

## 3. Mã Lỗi HTTP (HTTP Status Codes)

Client giao tiếp với hệ thống sẽ dựa 100% vào mã trạng thái HTTP. Bạn phải trả về đúng mã tương ứng với tình huống xử lý:

- **200 OK**: Request thành công và trả về dữ liệu (Thường dùng cho `GET`, `PUT`).
- **201 Created**: Dữ liệu mới đã được tạo thành công (Thường dùng cho `POST`, trả về kèm header `Location`).
- **204 No Content**: Xử lý thành công nhưng không có dữ liệu trả về (Thường dùng cho `DELETE`).
- **400 Bad Request**: Request sai định dạng, thiếu dữ liệu, hoặc vi phạm business logic (Validation).
- **401 Unauthorized**: Thiếu JWT Token, Token hết hạn, hoặc Token không hợp lệ.
- **403 Forbidden**: Token hợp lệ nhưng user **không có quyền (Permissions/Roles)** thực hiện hành động này.
- **404 Not Found**: Tài nguyên đang truy vấn không tồn tại trong Database.
- **409 Conflict**: Xung đột dữ liệu (Ví dụ: Email đã tồn tại, Sân đã có người đặt).
- **500 Internal Server Error**: Lỗi sập hệ thống không lường trước (Unhandled Exceptions). Lỗi này sẽ được Middleware gom lại và chuyển thành định dạng chuẩn `ProblemDetails` theo RFC 7807 để tránh lộ stacktrace.

---

## 4. Phân Quyền & Bảo Vệ Endpoint (RBAC)

Tất cả các API khi tạo mới **bắt buộc** phải tuân thủ chuẩn phân quyền tập trung bằng phương thức `.RequirePermission(AppPermissions.{Module}.{Action})`.

👉 **Xem chi tiết tài liệu hướng dẫn từng bước:** [Quy Tắc Phân Quyền RBAC khi tạo API](PERMISSION_GUIDELINES.vi.md)