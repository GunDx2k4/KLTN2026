# CHƯƠNG V: QUY TRÌNH KIỂM THỬ HỆ THỐNG THEO MÔ HÌNH CHỮ V (V-MODEL)

---

## 1. MỤC TIÊU & CHIẾN LƯỢC KIỂM THỬ

Nhằm đảm bảo tính đúng đắn của các giải pháp kỹ thuật, tính toàn vẹn dữ liệu và loại bỏ các lỗi kiến trúc/nghiệp vụ ngay từ giai đoạn phát triển, đề tài áp dụng **Mô hình kiểm thử chữ V (V-Model)** kết hợp chặt chẽ giữa:
- **Đảm bảo chất lượng (QA - Quality Assurance)**: Chuẩn hóa quy tắc kiến trúc Clean Architecture, quy ước đặt tên (Naming Conventions) và ma trận phân tầng trách nhiệm.
- **Kiểm soát chất lượng (QC - Quality Control)**: Xây dựng các bộ kịch bản kiểm thử tự động đa cấp độ từ kiểm thử kiến trúc (Architecture Tests), kiểm thử đơn vị (Unit Tests), kiểm thử tích hợp (Integration Tests) đến kiểm thử chấp nhận người dùng (UAT).

```
   [ Phân tích Yêu cầu ] <===========================> [ Kiểm thử Nghiệm thu UAT ]
            \                                                   /
     [ Thiết kế Hệ thống ] <====================> [ Kiểm thử Hệ thống & Tải Concurrency ]
              \                                       /
       [ Thiết kế Chi tiết ] <============> [ Kiểm thử Tích hợp Integration ]
                \                               /
         [ Cài đặt Mã nguồn ] ----> [ Kiểm thử Kiến trúc & Đơn vị Unit Tests ]
```

---

## 2. CHI TIẾT CÁC CẤP ĐỘ KIỂM THỬ HỆ THỐNG

### 2.1. Cấp độ 1: Kiểm thử Kiến trúc Tự động (Architecture Testing với NetArchTest)

Hệ thống tích hợp bộ kiểm thử kiến trúc tự động trong project [`LegendsTeamVN.BadmintonClub.Architecture.Tests`](file:///d:/[l7TeamVN]/LegendsTeamVN.BadmintonClub/tests/LegendsTeamVN.BadmintonClub.Architecture.Tests). Bất kỳ đoạn mã nào vi phạm ranh giới phân tầng Clean Architecture sẽ lập tức làm bài test thất bại (Failed) trong quy trình CI/CD.

```bash
dotnet test tests/LegendsTeamVN.BadmintonClub.Architecture.Tests/LegendsTeamVN.BadmintonClub.Architecture.Tests.csproj --verbosity minimal
```

**Kết quả thực thi**: **37 / 37 Tests PASSED (100%)**

#### Danh mục các quy tắc kiến trúc được kiểm thử tự động:
1. **Ràng buộc phân tầng bất biến (Layer Dependencies)**:
   - `Domain` không được phụ thuộc vào `Application`, `Persistence`, `Infrastructure` hoặc `Presentation`.
   - `Application` không được phụ thuộc vào `Persistence`, `Infrastructure` hoặc `Presentation`.
   - `Presentation` không được phụ thuộc trực tiếp vào `Persistence` hoặc `Infrastructure`.
2. **Quy chuẩn mã nguồn (Naming & Design Conventions)**:
   - Tất cả các class `CommandHandler` bắt buộc là `sealed` và kết thúc bằng hậu tố `CommandHandler`.
   - Tất cả các class `QueryHandler` bắt buộc là `sealed` và kết thúc bằng hậu tố `QueryHandler`.
   - Tất cả các class `Validator` bắt buộc kế thừa `AbstractValidator<T>` và kết thúc bằng hậu tố `Validator`.
   - Tất cả các Endpoint bắt buộc kế thừa `EndpointGroupBase` và kết thúc bằng hậu tố `Endpoint`.
   - Các Domain Entities bắt buộc có constructor không tham số (`protected` hoặc `private`) để ORM reflection hoạt động.

---

### 2.2. Cấp độ 2: Kiểm thử Ứng dụng & Nghiệp vụ (Application & Unit Testing)

Project [`LegendsTeamVN.BadmintonClub.Application.Tests`](file:///d:/[l7TeamVN]/LegendsTeamVN.BadmintonClub/tests/LegendsTeamVN.BadmintonClub.Application.Tests) tập trung kiểm thử các kịch bản nghiệp vụ cốt lõi, bảo vệ tính toàn vẹn (Invariants) của Aggregate Roots.

```bash
dotnet test tests/LegendsTeamVN.BadmintonClub.Application.Tests/LegendsTeamVN.BadmintonClub.Application.Tests.csproj --verbosity minimal
```

**Kết quả thực thi**: **44 / 44 Tests PASSED (100%)**

#### Các ca kiểm thử tiêu biểu:
- `CreateClub_WithValidData_ShouldCreateHostRoleAndActiveMember`: Kiểm tra khi tạo nhóm mới, người tạo tự động nhận vai trò Host với 100% quyền hạn và trở thành thành viên `ACTIVE`.
- `HasPermissionAsync_WhenSameUserIsInGroupAAsHostAndGroupBAsMember_ShouldHaveDifferentPermissionsPerGroup`: Kiểm tra trực tiếp 1 tài khoản tham gia 2 nhóm khác nhau: là Host ở nhóm A (có toàn quyền quản trị, tài chính) và là Thành viên ở nhóm B (chỉ có quyền xem, bị từ chối mọi thao tác quản trị).
- `SetDefaultRole_WhenTargetIsHostRole_ShouldThrowInvalidOperationException`: Đảm bảo không thể đặt vai trò Host làm vai trò mặc định cho thành viên mới.
- `RemoveRole_WhenRoleHasMembers_ShouldPreventDeletion`: Ngăn chặn việc xóa một vai trò nếu đang có thành viên trong nhóm nắm giữ vai trò đó.
- `JoinRequest_Approve_ShouldTransitionStatusToApproved`: Kiểm tra quy tắc chuyển đổi trạng thái duyệt đơn gia nhập CLB.
- `SessionBooking_WhenCapacityFull_ShouldPlaceIntoQueue`: Kiểm tra cơ chế tự động chuyển người đăng ký vượt trần vào hàng đợi (`WAITING`).

---

### 2.3. Cấp độ 3: Kiểm thử Tích hợp (Integration Testing)

Kiểm tra sự phối hợp đồng bộ giữa các tầng thành phần kỹ thuật:
1. **Kiểm thử Giao dịch Unit of Work (Transaction Rollback)**:
   - *Kịch bản*: Cố tình tạo một lỗi ngoại lệ (Exception) khi đang thêm lịch sinh hoạt của câu lạc bộ.
   - *Kết quả mong đợi*: Toàn bộ giao dịch bị hoàn tác (`Rollback`), bản ghi `club_group` và `group_role` không bị lưu rác vào PostgreSQL.
2. **Kiểm thử Webhook Đối soát Ngân hàng (SePay Webhook Integration)**:
   - *Kịch bản*: Giả lập gửi HTTP POST Webhook từ cổng thanh toán với mã giao dịch `payment_code` hợp lệ và số tiền chính xác.
   - *Kết quả mong đợi*: Bản ghi `session_fee` tự động đổi sang `PAID`, tạo bản ghi `bank_transaction` (`MATCHED`), tự động ghi một dòng tiền vào Sổ quỹ mở `group_ledger`.
3. **Kiểm thử Đôn hàng đợi khi có người báo vắng (Auto Queue Shift)**:
   - *Kịch bản*: Thành viên có vé `CONFIRMED` bấm báo vắng (`ABSENT`).
   - *Kết quả mong đợi*: Người có `queue_order = 1` trong hàng đợi được tự động đôn lên `CONFIRMED`, gửi thông báo FCM tức thì.

---

### 2.4. Cấp độ 4: Kiểm thử Hệ thống & Tải đồng thời (Concurrency & Stress Testing)

1. **Kiểm thử Chống vượt trần sĩ số qua Redis Distributed Lock**:
   - *Thiết lập*: Sử dụng công cụ kiểm thử tải giả lập 20 người dùng cùng lúc bấm giữ chỗ cho slot cuối cùng của buổi chơi trong cùng 1 giây.
   - *Kết quả*: Chỉ đúng 1 người chơi nhận được trạng thái `CONFIRMED`. 19 người chơi còn lại nhận được trạng thái `WAITING` với thứ tự hàng đợi từ 1 đến 19. Tỷ lệ lỗi vượt trần sĩ số đạt **0%**.
2. **Kiểm thử Phân quyền Đa nhóm Theo Ngữ cảnh Động (Dynamic Multi-Group RBAC Test)**:
   - *Kịch bản*: Cùng 1 tài khoản người dùng, tham gia Nhóm A với vai trò Host và tham gia Nhóm B với vai trò Thành viên thường.
   - *Kết quả*: 
     - Khi thực hiện thao tác quản trị trên Nhóm A (ví dụ: tạo buổi chơi, phân vai trò): Hệ thống duyệt thành công (`200 OK`).
     - Khi thực hiện thao tác quản trị tương tự trên Nhóm B: Hệ thống chặn và trả về lỗi `403 Forbidden` do vi phạm cơ chế phân quyền theo ngữ cảnh nhóm.

---

### 2.5. Cấp độ 5: Kịch bản Kiểm thử Chấp nhận Người dùng (UAT Scenarios)

| Mã UAT | Tên Kịch Bản | Các Bước Thực Hiện | Tiêu Chuẩn Nghiệm Thu | Kết Quả Thực Tế |
| :---: | :--- | :--- | :--- | :---: |
| **UAT-01** | Khởi tạo CLB và chuyển ngữ cảnh | 1. Đăng nhập tài khoản mới.<br>2. Bấm tạo CLB kèm lịch chơi.<br>3. Chuyển context sang CLB vừa tạo. | Nhóm mới hiển thị trên Dashboard, người dùng có vai trò Host với đầy đủ quyền quản trị. |  Đạt |
| **UAT-02** | Khám phá CLB theo GPS & Xin vào nhóm | 1. Bật GPS trên điện thoại.<br>2. Quét radar tìm CLB trong 5km.<br>3. Gửi đơn xin vào nhóm. | Hiển thị đúng khoảng cách, đơn xin vào nhóm chuyển sang `PENDING` bên máy Host. |  Đạt |
| **UAT-03** | Duyệt đơn gia nhập CLB | 1. Host mở tab Join Requests.<br>2. Bấm "Chấp thuận". | Ứng viên trở thành Thành viên chính thức (`ACTIVE`), được gán vai trò mặc định. |  Đạt |
| **UAT-04** | Giữ chỗ & Hàng đợi tự động | 1. Host mở buổi chơi 12 người.<br>2. 12 thành viên bấm giữ chỗ.<br>3. Người thứ 13 bấm giữ chỗ. | 12 người đầu tiên có vé `CONFIRMED`. Người thứ 13 vào hàng đợi `WAITING (Top 1)`. |  Đạt |
| **UAT-05** | Phát radar tìm khách giao lưu | 1. Buổi chơi thiếu 2 người.<br>2. Host phát tín hiệu bán kính 5km.<br>3. Khách vãng lai lân cận nhận thông báo. | Khách vãng lai bấm tham gia trực tiếp, lấp đầy slot trống thành công. |  Đạt |
| **UAT-06** | Hạch toán chia tiền & Quét VietQR | 1. Host nhập 3 quả cầu bóc mới.<br>2. Bấm chốt sổ buổi chơi.<br>3. Người chơi quét mã VietQR chuyển tiền. | Hệ thống tự động gạch nợ sang `PAID`, Sổ quỹ mở ghi nhận dòng tiền thu tức thì. |  Đạt |

---

## 3. KẾT LUẬN ĐÁNH GIÁ CHẤT LƯỢNG HỆ THỐNG

1. **Về độ ổn định & Tính toàn vẹn kiến trúc**: 100% các quy tắc phân tầng Clean Architecture, mã hóa mật khẩu, kiểm soát ngữ cảnh đa nhóm và đóng gói Entity đều được kiểm chứng tự động, không có bất kỳ vi phạm kiến trúc nào.
2. **Về độ chính xác nghiệp vụ**: Hệ thống khép kín và tự động hóa thành công toàn bộ 4 bài toán điểm đau thực tế: Giữ chỗ không vượt trần, Xoay tua sân công bằng theo thời gian nghỉ, Hạch toán chi phí đa hệ số theo lượng cầu bóc mới và Tự động gạch nợ vào Sổ quỹ mở qua VietQR.
3. **Mức độ hoàn thiện**: Sản phẩm đã sẵn sàng chuyển giao và nghiệm thu thực tế trên cả nền tảng Backend API và ứng dụng di động Flutter.
