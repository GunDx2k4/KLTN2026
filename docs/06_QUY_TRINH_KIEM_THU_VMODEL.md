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

Hệ thống tích hợp bộ kiểm thử kiến trúc tự động trong project [`LegendsTeamVN.BadmintonClub.Architecture.Tests`]. Bất kỳ đoạn mã nào vi phạm ranh giới phân tầng Clean Architecture sẽ lập tức làm bài test thất bại (Failed) trong quy trình CI/CD.

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

Project [`LegendsTeamVN.BadmintonClub.Application.Tests`] tập trung kiểm thử các kịch bản nghiệp vụ cốt lõi, bảo vệ tính toàn vẹn (Invariants) của Aggregate Roots.

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
| **UAT-06** | Hạch toán chia tiền, thanh toán và đối soát | 1. UC16 phát hành khoản.<br>2. UC17 xem phí/chọn phương thức/quét QR.<br>3. UC18 đối soát bằng chứng ngân hàng. | Phân biệt phát hành, tự báo UNPAID và xác nhận PAID/ledger; QR giữ đúng tiền/tài khoản/nội dung trên hai bank apps. | UC17 Backend/test tự động đạt; người dùng xác nhận hai app quét đúng. UC18 đã áp DB chung local và smoke API54796 ngày 04/11; chưa nghiệm thu toàn luồng vì thiếu SePay Test Mode/company/gateway thực, rollout server và FCM thiết bị thật. |

---

## 3. KẾT LUẬN ĐÁNH GIÁ CHẤT LƯỢNG HỆ THỐNG

1. **Về độ ổn định & Tính toàn vẹn kiến trúc**: 100% các quy tắc phân tầng Clean Architecture, mã hóa mật khẩu, kiểm soát ngữ cảnh đa nhóm và đóng gói Entity đều được kiểm chứng tự động, không có bất kỳ vi phạm kiến trúc nào.
2. **Về tài chính UC17**: Chỉ kết luận theo bằng chứng BE trên DB test riêng. Tự báo chuyển khoản/cash-request không chứng minh đã nhận tiền; test writer mô phỏng không phải webhook SePay. Không tuyên bố đã khép kín thanh toán/sổ quỹ qua VietQR.
3. **Mức độ hoàn thiện UC17**: Chưa nghiệm thu toàn bộ khi P03 hoặc hai bank apps chưa đạt, UC18/quy trình đối soát chưa sẵn sàng. FE/thiết bị/FCM thật, DB dùng chung/production và deploy chưa thực hiện trong đợt này.

## Bằng chứng UC17 Backend — cập nhật 01/11/2026

Các mốc 37/37 và 44/44 đầu tài liệu là số liệu lịch sử, không thay báo cáo đợt UC17. Kết quả hiện hành và lệnh chạy tại [UC17-BE.md](Detail%20Design/UC17-BE.md), ma trận trạng thái nghiệm thu ở mục Wave 3 của báo cáo đó.

- PostgreSQL riêng: migration thật từ baseline, Down sạch/Down bị guard, unique/FK/check, dữ liệu legacy và snapshot UC16, concurrency, lock timeout, lỗi SaveChanges và lỗi commit được chủ động tạo trong test.
- HTTP thật: JWT/ownership, guest/LEAVED/BANNED/disabled, đọc lịch sử khi Payments tắt, biên lai, bank config và guard pay cũ; cash/notice không tự PAID/ledger.
- Worker dùng provider mock: retry/partial failure/quyền bị thu hồi/PAID/dispatch/dead-letter và kind isolation. Không gửi FCM thật.
- POC Anpero bản vá metadata `1.0.1-uc17.1` và ảnh round-trip độc lập đạt. Người dùng xác nhận hai bank apps quét đúng, đã chạy request07 và cung cấp phần response phù hợp; tên/phiên bản app chưa cung cấp. UC17 đã nối DB chung local, kiểm backup/restore/migration/smoke; không đánh UAT-06 toàn luồng đạt vì UC18/đối soát/ledger thật chưa triển khai.

## Kiểm chứng UC18/FR21 — cập nhật 04/11/2026

Đã áp migration UC18 lên DB chung local PostgreSQL18.6/BadmintonClub sau backup và restore/Up/Down/Up trên instance PostgreSQL18 riêng; so sánh dữ liệu cũ 53 bảng trùng trước/sau. API54796 dùng profile CASH, qua 14 kiểm tra đọc/guard UC14–UC18; webhook/dispatch/seeder tắt. Không tạo giao dịch/PAID giả trên DB chung. Hai bank cũ thiếu BIN cần BankManage cấu hình trước cấp QR. DB server chưa áp/deploy; nghiệm thu SePay/FCM thực vẫn còn. Chi tiết và backup hash tại [UC18-BE](Detail%20Design/UC18-BE.md).

237 test liên quan ở lượt triển khai trước đã đạt: API/DB/hồi quy 102, hạ tầng 20, Domain UC16/UC17/UC18 115; kiểm kiến trúc 37/37 riêng. Build thành công, còn cảnh báo dependency/Firebase/seeder hiện hữu ngoài phần sửa. Đây không phải toàn bộ test của solution và không chứng minh đã nhận webhook thật.

Ma trận sandbox gồm HMAC/raw bytes/timestamp, payload key trùng, bank/group/header giả, quyền bị thu hồi khi chờ khóa, auto/manual/CASH, thiếu/thừa tiền, PAID lịch sử, reference collision, retry cùng ID/khác bằng chứng, sáu webhook đồng thời, hai giao dịch cùng fee, Redis mất kết nối/lease hết, commit lỗi và lỗi từng bảng bank/fee/ledger/notification/outbox. Toàn bộ rollback hoặc commit nguyên tử; kiểm cả FK/unique sau soft-delete và Down guard. FCM dùng mock, kiểm EventId/retry/dead-letter/thu hồi quyền giữa các batch.

Collection Postman phải chạy bằng runtime có Web Crypto, dùng năm fee riêng cho auto/manual/thiếu/thừa/CASH. Test runner bật/tắt webhook theo hai lượt, kiểm đủ 17 request cùng kết quả DB; không thay bằng fixture SePay. Backup/restore rehearsal chỉ trên hai DB fixture ngẫu nhiên, không dump/restore DB ứng dụng. Postman CLI đã chạy đủ 17 request và kiểm số nguồn tài chính sau chạy; backup/restore trên hai DB fixture riêng giữ nguyên năm nhóm dữ liệu và migration history. Xem [UC18-BE](Detail%20Design/UC18-BE.md).

UAT-06 toàn luồng tiếp tục chưa đạt cho tới khi có request Test Mode do provider phát hành, đúng company/gateway/tài khoản nhận, nghiệm thu notification trên thiết bị nếu tuyên bố push thật và rollout có duyệt đúng đích. Collection local, thông báo DB và FCM mock không thay bằng chứng đó.
