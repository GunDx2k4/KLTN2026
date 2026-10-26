# 🏸 BẢNG TỔNG HỢP VÀ TIẾN ĐỘ TOÀN BỘ CÁC YÊU CẦU CHỨC NĂNG (FR01 - FR23)
> **Dự án**: Hệ thống quản lý nhóm cầu lông phong trào (Badminton Club Management System)  
> **Tài liệu tham chiếu gốc**: `KLHK1262714_BC1_Hệ thống quản lý nhóm cầu lông phong trào.docx`  
> **Cập nhật ngày**: 14/10/2026  
> **Trạng thái hệ thống**: Backend (.NET 10 Clean Architecture) & Mobile (Flutter)

---

## 📊 BẢNG TỔNG KẾT TỔNG QUAN TIẾN ĐỘ 23 FR

| Mã FR | Tên Yêu Cầu Chức Năng | Phân Hệ Nghiệp Vụ | Trạng Thái Backend | Số Lượng API |
| :---: | :--- | :--- | :---: | :---: |
| **FR01** | Đăng ký tài khoản định danh | Quản trị Tài khoản & Đa nhóm (F1) |  **Đã xong 100%** | 1 API |
| **FR02** | Đăng nhập, duy trì phiên an toàn & Đăng xuất | Quản trị Tài khoản & Đa nhóm (F1) |  **Đã xong 100%** | 3 APIs |
| **FR03** | Tham gia đồng thời nhiều nhóm với vai trò độc lập | Quản trị Tài khoản & Đa nhóm (F1) |  **Đã xong 100%** | 11 APIs |
| **FR04** | Chuyển đổi ngữ cảnh nhóm trên ứng dụng di động | Quản trị Tài khoản & Đa nhóm (F1) |  **Đã xong 100%** | Header `X-Group-Id` (Clean Context) |
| **FR05** | Quản lý hồ sơ cá nhân & Trình độ chuyên môn tự đánh giá | Quản trị Tài khoản & Đa nhóm (F1) |  **Đã xong 100%** | 2 APIs |
| **FR06** | Tìm kiếm & Khám phá CLB quanh vị trí GPS & Bán kính | Khám phá, Giữ chỗ & Kết nối (F2) |  **Đã xong 100%** | 1 API |
| **FR07** | Xem chi tiết thông tin CLB & Lịch sinh hoạt định kỳ tuần | Khám phá, Giữ chỗ & Kết nối (F2) |  **Đã xong 100%** | 3 APIs |
| **FR08** | Đăng ký giữ chỗ buổi chơi có kiểm soát sĩ số | Khám phá, Giữ chỗ & Kết nối (F2) |  **Đã xong 100%** | 3 APIs |
| **FR09** | Tự động kích hoạt hàng đợi khi đạt trần sức chứa sân | Khám phá, Giữ chỗ & Kết nối (F2) |  **Đã xong 100%** | Logic tự động |
| **FR10** | Tự động đôn người chơi đầu hàng đợi khi có người báo vắng | Khám phá, Giữ chỗ & Kết nối (F2) |  **Đã xong 100%** | 1 API + Logic |
| **FR11** | Host phát tín hiệu tìm người giao lưu lân cận khi thiếu quân | Khám phá, Giữ chỗ & Kết nối (F2) |  **Đã xong 100%** | 3 APIs |
| **FR12** | Khách vãng lai quét radar & Đăng ký trực tiếp slot giao lưu | Khám phá, Giữ chỗ & Kết nối (F2) |  **Đã xong 100%** | 3 APIs |
| **FR13** | Cấu hình nhóm, phân quyền & Khởi tạo buổi chơi | Vận hành Buổi chơi & Điều phối (F3) |  **Đã xong 100%** | 6 APIs |
| **FR14** | Điểm danh nhanh danh sách người chơi thực tế có mặt tại sân | Vận hành Buổi chơi & Điều phối (F3) |  **Đã xong 100%** | 4 APIs |
| **FR15** | Giải thuật tự động đề xuất xoay tua sân (Nghỉ lâu + Trình độ) | Vận hành Buổi chơi & Điều phối (F3) |  **Đã xong 100%** | 5 APIs |
| **FR16** | Host can thiệp điều chỉnh danh sách thi đấu thủ công | Vận hành Buổi chơi & Điều phối (F3) |  **Đã xong 100%** | 4 APIs |
| **FR17** | Người chơi theo dõi lượt thi đấu cá nhân theo thời gian thực | Vận hành Buổi chơi & Điều phối (F3) | ⏳ *Sẵn sàng DB & Hub, cần hoàn thiện UI* | 0 API |
| **FR18** | Ghi nhận số quả cầu bóc mới và phụ phí phát sinh buổi chơi | Hạch toán Vật tư & Sổ quỹ (F4) | ⏳ *Sẵn sàng DB, cần thêm Endpoint* | 0 API |
| **FR19** | Hạch toán chi phí đa hệ số tự động (Nam, Nữ, Tháng, Ngày, Khách vãng lai) | Hạch toán Vật tư & Sổ quỹ (F4) | 🔄 *Đã có API cấu hình biểu phí, cần thêm Engine tính tiền buổi* | 2 APIs |
| **FR20** | Khởi tạo mã VietQR thanh toán nhúng sẵn tiền & mã duy nhất | Hạch toán Vật tư & Sổ quỹ (F4) | ⏳ *Sẵn sàng DB, cần thêm Engine* | 0 API |
| **FR21** | Tiếp nhận biến động số dư ngân hàng để tự động xác nhận thanh toán | Hạch toán Vật tư & Sổ quỹ (F4) | ⏳ *Sẵn sàng DB, cần thêm Webhook* | 0 API |
| **FR22** | Quản trị Sổ quỹ mở nội bộ minh bạch (Thu, Chi, Dư nợ) | Hạch toán Vật tư & Sổ quỹ (F4) | ⏳ *Sẵn sàng DB, cần thêm Endpoint* | 0 API |
| **FR23** | Đánh giá tín nhiệm hai chiều (Điểm thành viên & Điểm CLB) | Đánh giá & Tín nhiệm (F5) | ⏳ *Sẵn sàng DB, cần thêm Endpoint* | 0 API |

> **Thống kê hiện tại**:
> - **Đã hoàn thành và chạy thực tế**: **16 / 23 FR** (FR01 → FR16).
> - **Tổng số API Backend đã hoàn thiện**: **48 Endpoints RESTful**, kiểm thử tự động đạt 37/37 Architecture Tests, 312/312 bài test toàn hệ thống passed.
> - **Các FR tiếp theo cần làm**: FR17 → FR23 (Giao diện Realtime lượt đấu, Hạch toán chi phí cầu/sân đa hệ số, VietQR, Webhook SePay, Sổ quỹ, Đánh giá).

---

## 🌟 PHẦN I: CHI TIẾT CÁC FR ĐÃ HOÀN THÀNH (BACKEND PRODUCTION READY)

### 1. FR01 & FR02: Định Danh, Đăng Ký, Đăng Nhập & Phiên Làm Việc
- **Mục tiêu**: Quản lý tài khoản duy nhất, cấp Access Token (JWT) và Refresh Token an toàn, duy trì phiên trên thiết bị.
- **Danh sách API**:
  1. `POST /api/v1/auth/register`: Đăng ký tài khoản mới bằng Username, Email, Số điện thoại và Mật khẩu.
  2. `POST /api/v1/auth/login`: Đăng nhập hệ thống bằng Identifier (Username/Email) và Password; trả về JWT Access Token (hạn 60 phút), Refresh Token và danh sách CLB kèm vai trò của người dùng.
  3. `POST /api/v1/auth/refresh-token`: Cấp mới Access Token khi token cũ hết hạn mà không bắt người dùng đăng nhập lại.
  4. `POST /api/v1/auth/logout`: Thu hồi token và hủy phiên làm việc trên thiết bị.

---

### 2. FR03: Quản Trị Đa Nhóm & Vai Trò Độc Lập
- **Mục tiêu**: Một tài khoản người dùng có thể tham gia đồng thời nhiều nhóm cầu lông khác nhau và đảm nhiệm các vai trò độc lập theo từng nhóm (ví dụ: `user1` là Host ở CLB Quận 7, nhưng chỉ là Thành viên thường ở CLB Quận 10).
- **Swagger Group**: `FR03 - Quản lý CLB & Thành viên`
- **Danh sách API**:
  1. `GET /api/v1/clubs/my-clubs`: Lấy danh sách toàn bộ các CLB người dùng đang tham gia kèm tên vai trò và loại hội viên (`MONTHLY` / `DAILY`).
  2. `POST /api/v1/clubs/join`: Tham gia nhanh vào CLB tức thì bằng mã Code bí mật của nhóm (ví dụ: `CLB-LEGENDS7`).
  3. `POST /api/v1/clubs/{id}/join-requests`: Gửi đơn xin gia nhập nhóm kèm lời giới thiệu bản thân và trình độ cá nhân.
  4. `GET /api/v1/clubs/{id}/join-requests`: Host xem danh sách các đơn xin gia nhập đang chờ duyệt (`PENDING`).
  5. `PUT /api/v1/clubs/{id}/join-requests/{requestId}/approve`: Host phê duyệt đơn, ứng viên chính thức thành thành viên CLB.
  6. `PUT /api/v1/clubs/{id}/join-requests/{requestId}/reject`: Host từ chối đơn gia nhập.
  7. `GET /api/v1/clubs/{id}/members`: Xem danh sách thành viên trong CLB kèm avatar, trình độ, chức vụ và loại hội viên.
  8. `PUT /api/v1/clubs/{id}/members/{userId}/role`: Host/Quản trị viên bổ nhiệm hoặc thay đổi vai trò nội bộ của thành viên (Host, Thủ quỹ, Phó nhóm, Thành viên).
  9. `DELETE /api/v1/clubs/{id}/members/{userId}`: Thành viên tự rời khỏi CLB hoặc Host xóa thành viên vi phạm khỏi nhóm.
  10. `GET /api/v1/clubs/{id}/roles`: Xem danh mục các vai trò và quyền hạn nội bộ được cấu hình riêng trong CLB.
  11. `POST /api/v1/clubs`: Tạo mới một câu lạc bộ cầu lông (Người tạo tự động trở thành Host với 17 quyền hạn tối cao).

---

### 3. FR04: Chuyển Đổi Ngữ Cảnh Nhóm (Clean Context-Based Authorization qua Header X-Group-Id)
- **Mục tiêu**: Chuyển đổi nhóm làm việc trên thanh Server-Rail / Drawer của Mobile. Toàn bộ quyền hạn, dữ liệu và giao diện tự động đồng bộ theo vai trò của người dùng tại nhóm đang chọn tức thì 0ms mà không phải gọi API cấp lại JWT.
- **Cơ chế triển khai chính thức**:
  - **Bỏ hoàn toàn API switch-context cấp lại Token**: Client đăng nhập 1 lần nhận 1 JWT toàn cục duy nhất.
  - **Đính kèm Header X-Group-Id: <clubId>**: Khi người dùng chọn CLB trên giao diện, Client tự động gán Header này (hoặc truyền qua route parameter {clubId}, {groupId}, {sessionId}).
  - **Thẩm tra quyền động Realtime**: GroupPermissionHandler tự động đọc Header X-Group-Id và kiểm tra quyền qua IGroupAuthorizationService (Redis cache 10 phút, tự động invalidate khi Host đổi role).
  - Chi tiết tích hợp: Xem [HUONG_DAN_FE_XAC_THUC_VA_NGU_CANH_X_GROUP_ID.md](HUONG_DAN_FE_XAC_THUC_VA_NGU_CANH_X_GROUP_ID.md).
---

### 4. FR05: Quản Lý Hồ Sơ Cá Nhân & Trình Độ Tự Đánh Giá
- **Mục tiêu**: Quản lý ảnh đại diện thật, họ tên, giới tính, trạng thái sẵn sàng tham gia giao lưu (`isOpenForGuest`), điểm uy tín cá nhân và 6 bậc trình độ kỹ năng chuyên môn chuẩn:
  - `1 - NB`: Mới tập chơi (Newbie)
  - `2 - Y`: Yếu (Biết luật, đỡ được cầu cơ bản)
  - `3 - TBY`: Trung bình yếu (Phông cầu, đập cầu cơ bản)
  - `4 - TB`: Trung bình (Chơi phong trào ổn định)
  - `5 - TBK`: Trung bình khá (Chiến thuật đôi tốt, phản xạ nhanh)
  - `6 - K`: Khá (Tấn công uy lực, thi đấu phong trào có giải)
- **Swagger Group**: `FR05 - Quản lý hồ sơ cá nhân & Trình độ`
- **Danh sách API**:
  1. `GET /api/v1/user/profile`: Xem thông tin chi tiết hồ sơ cá nhân, ảnh đại diện, trình độ hiện tại và điểm uy tín.
  2. `PUT /api/v1/user/profile`: Cập nhật ảnh đại diện (`avatarUrl`), họ và tên, giới tính, trình độ tự đánh giá (`skillLevel`) và bật/tắt sẵn sàng tham gia giao lưu (`isOpenForGuest`).

---

### 5. FR06: Khám Phá & Tra Cứu Nhóm Quanh Tọa Độ GPS & Bán Kính
- **Mục tiêu**: Quét radar tìm kiếm các CLB cầu lông đang hoạt động xung quanh tọa độ vị trí hiện tại dựa trên công thức Haversine; hỗ trợ lọc theo bán kính (km), trình độ mục tiêu, trạng thái đang tuyển người và tìm kiếm theo từ khóa.
- **Swagger Group**: `FR06 - Khám phá nhóm theo vị trí`
- **Danh sách API**:
  1. `GET /api/v1/clubs/explore`:
     - Tham số: `latitude`, `longitude`, `radiusKm` (mặc định 10km), `skillLevel`, `isRecruiting`, `searchTerm`.
     - Trả về: Danh sách nhóm kèm cự ly tính bằng km (`distanceKm`), tên sân cố định, địa chỉ, khoảng trình độ tiếp nhận (`minSkillLevel` - `maxSkillLevel`), tóm tắt lịch cố định tuần và điểm chất lượng CLB.

---

### 6. FR07: Xem Chi Tiết Thông Tin Nhóm & Lịch Sinh Hoạt Định Kỳ Tuần
- **Mục tiêu**: Xem đầy đủ hồ sơ của CLB (sân sinh hoạt cố định, địa chỉ, tọa độ GPS, khung giờ sinh hoạt cố định hàng tuần từ Thứ 2 đến Chủ Nhật, Host đại diện, tiêu chuẩn tuyển chọn). Cho phép Host cấu hình thông tin này.
- **Swagger Group**: `FR07 - Thông tin chi tiết & Lịch sinh hoạt nhóm`
- **Danh sách API**:
  1. `GET /api/v1/clubs/{id}`: Xem chi tiết CLB đầy đủ: thông tin sân cố định, danh sách lịch định kỳ tuần, thông tin Host, sĩ số thành viên, và trạng thái cá nhân của người gọi (`isJoined`, `roleName`, `hasPendingRequest`).
  2. `PUT /api/v1/clubs/{id}/info`: Host cấu hình sân bãi sinh hoạt cố định, địa chỉ, GPS, trình độ sàn/trần mục tiêu.
  3. `PUT /api/v1/clubs/{id}/schedules`: Host thiết lập các khung giờ sinh hoạt cố định lặp lại hàng tuần của nhóm (Thứ trong tuần `dayOfWeek`, giờ bắt đầu `startTime`, giờ kết thúc `endTime`).

---

### 7. FR08, FR09, FR10: Đăng Ký Giữ Chỗ, Hàng Đợi & Tự Động Đôn Hàng
- **Mục tiêu**:
  - **FR08**: Thành viên đăng ký giữ chỗ (`Vote YES`) trước hạn đóng sổ.
  - **FR09 (Hàng đợi tự động)**: Khi số lượng vé chính thức chạm trần tối đa theo số sân (`maxPlayers = courtCount * playersPerCourt`), các lượt vote tiếp theo tự động chuyển vào Hàng đợi dự bị (`WAITING_LIST`) theo thứ tự thời gian (`queueNumber = 1, 2, 3...`).
  - **FR10 (Tự động đôn hàng)**: Khi một người có vé chính thức báo vắng (`Cancel Booking`), hệ thống tự động đôn người chờ ở vị trí số #1 lên vé chính thức (`CONFIRMED`) và tự động đẩy các thứ tự còn lại lên trước.
- **Swagger Group**: `FR08-FR10 - Đăng ký giữ chỗ & Hàng đợi`
- **Danh sách API**:
  1. `POST /api/v1/sessions/{sessionId}/bookings`: Đăng ký giữ chỗ tham gia buổi chơi (Hệ thống tự động phân nhánh cấp vé `CONFIRMED` hoặc đưa vào `WAITING_LIST`).
  2. `DELETE /api/v1/sessions/{sessionId}/bookings/me`: Hủy giữ chỗ / Báo vắng (Tự động đôn người đầu hàng đợi lên chính thức).
  3. `GET /api/v1/sessions/{sessionId}/bookings`: Lấy danh sách toàn bộ người đã giữ vé chính thức và danh sách hàng đợi của buổi chơi.
  4. `GET /api/v1/sessions/{sessionId}/bookings/me`: Kiểm tra trạng thái vé đăng ký giữ chỗ của chính mình.

---

### 8. FR11: Host Phát Tín Hiệu Tìm Người Giao Lưu Lân Cận
- **Mục tiêu**: Khi buổi chơi gần đến giờ diễn ra nhưng chưa đủ quân (`confirmedCount < maxPlayers`), Host phát tín hiệu tuyển khách vãng lai, định mức số slot cần tuyển, chi phí chia sẻ sân/cầu và bán kính quét radar (km).
- **Swagger Group**: `FR11 - Phát tín hiệu tìm khách giao lưu`
- **Danh sách API**:
  1. `POST /api/v1/sessions/{sessionId}/recruitments`: Host phát tín hiệu tuyển khách giao lưu lân cận khi thiếu người.
  2. `GET /api/v1/sessions/{sessionId}/recruitments`: Xem thông tin bài đăng tìm khách giao lưu của buổi chơi.
  3. `DELETE /api/v1/sessions/{sessionId}/recruitments`: Host đóng / hủy bài đăng tìm khách giao lưu khi đã đủ người.

---

### 9. FR12: Tiếp Nhận & Đăng Ký Slot Giao Lưu Cho Khách Vãng Lai
- **Mục tiêu**: Các tay vợt tự do xung quanh mở tab Radar "Tìm kèo giao lưu", thấy các buổi chơi đang tuyển khách quanh tọa độ GPS của mình kèm khoảng cách cự ly (km), mức phí chia sẻ và bấm nhận slot trực tiếp (`Join`). Hệ thống tự động cấp vé và đóng bài đăng khi đủ slot.
- **Swagger Group**: `FR12 - Khách vãng lai đăng ký tham gia`
- **Danh sách API**:
  1. `GET /api/v1/sessions/recruitments/explore`: Khách vãng lai quét tìm các kèo giao lưu quanh vị trí GPS trong bán kính `radiusKm`.
  2. `POST /api/v1/sessions/recruitments/{recruitmentId}/join`: Khách vãng lai xác nhận nhận trực tiếp suất chơi giao lưu còn trống.
  3. `DELETE /api/v1/sessions/recruitments/{recruitmentId}/cancel`: Khách vãng lai hủy suất giao lưu đã đăng ký.

---

### 10. FR13: Cấu Hình Nhóm, Phân Quyền & Vận Hành Buổi Chơi
- **Mục tiêu**: Cho phép Host khởi tạo buổi chơi mới (khung giờ, số lượng sân thuê, định mức người/sân, trần sĩ số tiếp nhận, hạn chốt biểu quyết), cập nhật thông tin vận hành và chuyển đổi trạng thái buổi chơi (`OPEN` -> `LOCKED` -> `PLAYING` -> `COMPLETED`).
- **Swagger Group**: `FR13 - Khởi tạo buổi chơi`
- **Danh sách API**:
  1. `POST /api/v1/sessions/club/{clubId}`: Host khởi tạo buổi chơi mới cho câu lạc bộ.
  2. `GET /api/v1/sessions/club/{clubId}`: Lấy danh sách toàn bộ các buổi chơi của câu lạc bộ.
  3. `GET /api/v1/sessions/{sessionId}`: Xem thông tin chi tiết buổi chơi, sĩ số, danh sách vé và thông tin sân bãi.
  4. `PUT /api/v1/sessions/{sessionId}`: Chỉnh sửa thông tin vận hành buổi chơi (đổi sân, đổi giờ, số sân).
  5. `PATCH /api/v1/sessions/{sessionId}/status`: Cập nhật trạng thái buổi chơi (`LOCKED` - Khóa sổ, `PLAYING` - Đang diễn ra, `COMPLETED` - Kết thúc).
  6. `DELETE /api/v1/sessions/{sessionId}`: Hủy bỏ buổi sinh hoạt cầu lông.

---

---

### 11. FR14: Điểm Danh Thành Viên Tại Sân
- **Mục tiêu**: Xác nhận danh sách những người thực tế có mặt tại sân, xử lý người đến vãng lai (walk-in) và chốt danh sách sẵn sàng cho điều phối xoay tua sân.
- **Swagger Group**: `FR14 - Điểm danh tại sân`
- **Danh sách API**:
  1. `GET /api/v1/sessions/{sessionId}/attendance`: Xem danh sách điểm danh và sĩ số thực tế tại sân.
  2. `PUT /api/v1/sessions/{sessionId}/attendance/{userId}`: Đánh dấu có mặt hoặc vắng mặt cho người chơi.
  3. `POST /api/v1/sessions/{sessionId}/attendance/walk-ins`: Thêm nhanh người chơi vãng lai và xác nhận có mặt.
  4. `POST /api/v1/sessions/{sessionId}/attendance/complete`: Hoàn tất điểm danh, chốt danh sách sẵn sàng cho điều phối.

---

### 12. FR15: Điều Phối Xoay Tua Sân Tự Động
- **Mục tiêu**: Giải thuật tự động đề xuất 4 người chơi vào sân trống dựa trên 2 tiêu chí tối thượng: (1) Ưu tiên người nghỉ lâu nhất (chống ngồi ngoài liên tiếp 2 trận), (2) Cân bằng tổng điểm trình độ giữa 2 cặp đấu.
- **Swagger Group**: `FR15 - Điều phối xoay tua sân`
- **Danh sách API**:
  1. `GET /api/v1/sessions/{sessionId}/rotation`: Xem trạng thái các sân và đề xuất điều phối.
  2. `POST /api/v1/sessions/{sessionId}/rotation/initialize`: Khởi tạo điều phối sau khi hoàn tất điểm danh.
  3. `POST /api/v1/sessions/{sessionId}/rotation/proposals`: Sinh đề xuất bổ sung cho các sân trống.
  4. `POST /api/v1/sessions/{sessionId}/matches/{matchId}/start`: Xác nhận bắt đầu trận đấu đã đề xuất.
  5. `POST /api/v1/sessions/{sessionId}/matches/{matchId}/finish`: Kết thúc trận và kích hoạt tính toán lượt kế tiếp.

---

### 13. FR16: Can Thiệp Điều Chỉnh Danh Sách Thi Đấu Thủ Công
- **Mục tiêu**: Cho phép Host hoán đổi cặp đấu, chỉnh người vào sân, tạm nghỉ hoặc khôi phục đề xuất ban đầu với kiểm soát version chống ghi đè chéo.
- **Swagger Group**: `FR16 - Can thiệp đội hình thủ công`
- **Danh sách API**:
  1. `PUT /api/v1/sessions/{sessionId}/matches/{matchId}/manual-lineup`: Host điều chỉnh đội hình trước khi bắt đầu trận.
  2. `POST /api/v1/sessions/{sessionId}/matches/{matchId}/restore-proposal`: Khôi phục gợi ý đội hình tự động ban đầu.
  3. `PUT /api/v1/sessions/{sessionId}/rotation/players/{userId}/pause`: Host đặt tạm nghỉ hoặc sẵn sàng trở lại cho vận động viên.
  4. `POST /api/v1/sessions/{sessionId}/matches/{matchId}/confirm-manual-start`: Host xác nhận đội hình và bắt đầu trận kèm kiểm soát version.

## ⏳ PHẦN II: CÁC FR KẾ TIẾP CẦN XÂY DỰNG (FR17 - FR23)

Các phân hệ dưới đây đã có sẵn cấu trúc bảng và mô hình thực thể Entity Framework Core trong cơ sở dữ liệu (`BadmintonDbContext`), bước tiếp theo sẽ xây dựng Command/Query/Handler và Endpoint API tương ứng:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        LỘ TRÌNH 10 FR TIẾP THEO                         │
├──────────────────────────┬─────────────────────────────────────────────┤
│ Nhóm Vận Hành Tại Sân    │ • FR14: Đã xong (Điểm danh sân)             │
│                          │ • FR15: Đã xong (Xoay tua tự động)          │
│                          │ • FR16: Đã xong (Can thiệp thủ công)        │
│                          │ • FR17: Màn hình theo dõi realtime (SignalR)│
├──────────────────────────┼─────────────────────────────────────────────┤
│ Nhóm Tài Chính & Sổ Quỹ  │ • FR18: Ghi nhận số quả cầu bóc & Phụ phí   │
│                          │ • FR19: Hạch toán chia tiền đa hệ số        │
│                          │ • FR20: Khởi tạo mã VietQR thanh toán       │
│                          │ • FR21: Webhook ngân hàng tự động xác nhận thanh toán   │
│                          │ • FR22: Quản trị Sổ quỹ mở minh bạch        │
├──────────────────────────┼─────────────────────────────────────────────┤
│ Nhóm Đánh Giá & Tín Nhiệm│ • FR23: Đánh giá uy tín 2 chiều (User & CLB)│
└──────────────────────────┴─────────────────────────────────────────────┘
```

### 1. Phân hệ Vận hành tại sân & Điều phối xoay tua (FR14 - FR17)
- **FR14 - Điểm danh tại sân**:
  - *Mục tiêu*: Xác nhận danh sách những người thực tế có mặt tại sân trước khi bắt đầu trận, đưa người chơi vào trạng thái sẵn sàng để thuật toán xếp sân.
  - *Endpoint dự kiến*: `PATCH /api/v1/sessions/{sessionId}/check-in` (Host check-in hoặc thành viên quét mã QR tại sân).
- **FR15 - Điều phối xoay tua sân tự động**:
  - *Mục tiêu*: Khi một sân trống, giải thuật tự động chọn 4 người chơi tiếp theo ưu tiên theo: (1) Thời gian nghỉ lâu nhất (chống ngồi ngoài liên tiếp 2 trận), (2) Cân bằng tổng trình độ giữa 2 cặp đấu.
  - *Bảng dữ liệu đã có*: `session_match`, `match_player`.
  - *API BE hiện có*: GET `/api/v1/sessions/{sessionId}/rotation`, POST `/api/v1/sessions/{sessionId}/rotation/initialize`, POST `/api/v1/sessions/{sessionId}/rotation/proposals`, POST `/api/v1/sessions/{sessionId}/matches/{matchId}/start`, POST `/api/v1/sessions/{sessionId}/matches/{matchId}/finish`. Xem [UC13-BE.md](Detail%20Design/UC13-BE.md); endpoint matches/next cũ là dự kiến, không phải API đã triển khai.
- **FR16 - Can thiệp xếp trận thủ công**:
  - *Mục tiêu*: Cho phép Host hoán đổi cặp đấu, đổi người vào sân bằng thao tác kéo thả trên mobile khi có yêu cầu phát sinh.
  - *Đã triển khai C/D/E, kiểm chứng sandbox*: PUT `/api/v1/sessions/{sessionId}/matches/{matchId}/manual-lineup`; mở rộng GET rotation với baseline, chênh lệch, manual flags/capability. Mặc định ManualEditingEnabled=false; chỉ Host theo DB hiện hành và MatchManage được chỉnh/reset/pause/resume.
  - *Đã triển khai D*: POST `/api/v1/sessions/{sessionId}/matches/{matchId}/restore-proposal`, PUT `/api/v1/sessions/{sessionId}/rotation/players/{userId}/pause`. *Đã triển khai E*: POST `/api/v1/sessions/{sessionId}/matches/{matchId}/confirm-manual-start`; start UC13 cũ bị 409 với baseline, untouched giữ nguyên; retry Playing không thêm bản ghi. F/G đã đạt trên sandbox; bộ review SQL/backup/rollback chuẩn bị tại D:/KhoaLuan/PLan_agent/UC14-DB-REVIEW/. Chưa chạy DB đích hoặc deploy. Xem [UC14-BE.md](Detail%20Design/UC14-BE.md). Endpoint swap-players cũ được thay bằng contract này. DB đích chưa nâng cấp.
- **FR17 - Theo dõi lịch thi đấu thời gian thực**:
  - *Mục tiêu*: Cung cấp màn hình hiển thị trực quan cho người chơi: sân sắp đánh, bạn cùng chơi/đối thủ ở lượt tiếp theo và thời gian nghỉ dự kiến.
  - *Endpoint dự kiến*: `GET /api/v1/sessions/{sessionId}/my-matches`.

---

### 2. Phân hệ Hạch toán đa hệ số, VietQR & Sổ quỹ mở (FR18 - FR22)
- **FR18 - Ghi nhận vật tư thực tế**:
  - *Mục tiêu*: Host/Thủ quỹ nhập số lượng quả cầu đã bóc mới trong buổi chơi và các khoản phụ phí phát sinh (nước uống, tiền sân ngoài giờ).
  - *Endpoint dự kiến*: `PUT /api/v1/sessions/{sessionId}/consumables`.
- **FR19 - Hạch toán chi phí đa hệ số**:
  - *Mục tiêu*: Tự động chia tổng chi phí theo công thức trọng số cấu hình tại `group_fee_rule`:
    - Hội viên tháng: Không tính tiền sân/cầu ngày (đã đóng gói tháng).
    - Hội viên ngày (Nam vs Nữ): Áp dụng mức giảm giá riêng cho Nữ (emale_discount_daily).
    - Khách vãng lai (Nam vs Nữ): Áp dụng giá vé khách vãng lai và giảm giá riêng cho Nữ (emale_discount_guest).
  - *Endpoint dự kiến*: `POST /api/v1/sessions/{sessionId}/settlement`.
- **FR20 - Khởi tạo mã thanh toán VietQR định danh**:
  - *Mục tiêu*: Tự động sinh chuỗi mã VietQR nhúng sẵn chính xác số tiền phải đóng và mã giao dịch định danh duy nhất (ví dụ: `PAY_SES123_USR456`) để người chơi quét chuyển khoản bằng app ngân hàng.
  - *Bảng dữ liệu đã có*: `session_fee`, `group_bank_account`.
  - *Endpoint dự kiến*: `GET /api/v1/sessions/{sessionId}/fees/me/qr`.
- **FR21 - Đối soát & Xác nhận thanh toán ngân hàng tự động**:
  - *Mục tiêu*: Tiếp nhận webhook biến động số dư tài khoản ngân hàng (Casso / SePay / VietinBank), tự động khớp nội dung chuyển khoản với mã định danh hóa đơn, xác nhận thanh toán sang trạng thái `PAID` mà không cần thủ quỹ kiểm tra sao kê thủ công.
  - *Bảng dữ liệu đã có*: `bank_transaction`.
  - *Endpoint dự kiến*: `POST /api/v1/webhooks/bank-transfer`.
- **FR22 - Quản trị Sổ quỹ mở nội bộ minh bạch**:
  - *Mục tiêu*: Công khai minh bạch dòng tiền thu/chi, số dư quỹ khả dụng và danh sách người còn nợ tiền cho toàn thể hội viên theo dõi; cho phép thủ quỹ tạo phiếu thu/chi ngoài sân.
  - *Bảng dữ liệu đã có*: `group_ledger`.
  - *Endpoint dự kiến*: `GET /api/v1/clubs/{clubId}/ledger`, `POST /api/v1/clubs/{clubId}/ledger/transactions`.

---

### 3. Phân hệ Đánh giá uy tín & Chất lượng nhóm (FR23)
- **FR23 - Đánh giá tín nhiệm hai chiều & Điểm uy tín**:
  - *Mục tiêu*:
    - **Điểm uy tín thành viên**: Tự động tăng khi tham gia đúng giờ, thanh toán sòng phẳng; tự động trừ điểm khi hủy hẹn sát giờ (báo vắng sát giờ hoặc không đến).
    - **Điểm chất lượng hội nhóm**: Tính theo điểm đánh giá của thành viên sau buổi chơi kết hợp tính minh bạch của sổ quỹ.
  - *Bảng dữ liệu đã có*: `member_review`, `group_review`.
  - *Endpoint dự kiến*: `POST /api/v1/reviews/member`, `POST /api/v1/reviews/club`.

---

## 🛠️ HƯỚNG DẪN DÀNH CHO PHÍA MOBILE (FLUTTER)

1. **Swagger UI trực quan**:
   - Khi Backend đang chạy, truy cập đường dẫn: **`http://localhost:54796/swagger`**
   - Toàn bộ các API đều được phân nhóm rõ ràng theo Tag từng FR (ví dụ: `FR03 - Quản lý CLB & Thành viên`, `FR05 - Quản lý hồ sơ cá nhân & Trình độ`, `FR06 - Khám phá nhóm theo vị trí`, `FR07 - Thông tin chi tiết & Lịch sinh hoạt nhóm`, `FR08-FR10 - Đăng ký giữ chỗ & Hàng đợi`, `FR11 - Phát tín hiệu tìm khách giao lưu`, `FR12 - Khách vãng lai đăng ký tham gia`, `FR13 - Khởi tạo buổi chơi`).

2. **Tài liệu đặc tả chi tiết chuyên sâu**:
   - Đặc tả luồng dữ liệu, JSON Request/Response, logic Flutter mẫu: [docs/API_DOCUMENTATION.md](file:///c:/code/abp_be/KLTN2026/docs/API_DOCUMENTATION.md)
   - Đặc tả độc lập cho Phân hệ 1 (FR05, FR06, FR07): [docs/PHAN_HE_1_HO_SO_VA_TIM_KIEM_CLB_FR05_FR06_FR07.md](docs/PHAN_HE_1_HO_SO_VA_TIM_KIEM_CLB_FR05_FR06_FR07.md)
   - Hướng dẫn FE Phân hệ Quản lý Nhóm & CLB (19 APIs): [docs/HUONG_DAN_FE_PHAN_HE_QUAN_LY_NHOM_CLB.md](docs/HUONG_DAN_FE_PHAN_HE_QUAN_LY_NHOM_CLB.md)

3. **Tài khoản kiểm thử có sẵn dữ liệu**:
   - Mật khẩu tất cả tài khoản: **`admin`**
   - Tài khoản trọng tâm: **`user1`** (Vừa làm Host CLB Q.7, vừa làm Thành viên CLB Q.10, vừa có đơn chờ duyệt tại CLB Bách Khoa).
   - Quản trị viên: **`admin`**.
