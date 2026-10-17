# CHƯƠNG II: KHẢO SÁT & ĐẶC TẢ YÊU CẦU HỆ THỐNG

---

## 1. MỤC ĐÍCH & MỤC TIÊU KHẢO SÁT

### 1.1. Mục đích
Khảo sát nhằm thu thập dữ liệu định lượng và định tính từ hai nhóm tác nhân chính của hệ sinh thái cầu lông phong trào:
1. **Ban điều hành hội nhóm (Host / Quản lý / Thủ quỹ)**: Thu thập quy trình chốt danh sách người tham gia, cơ chế sắp xếp cặp đấu tại sân, cách ghi nhận tiêu hao quả cầu lông và rủi ro nợ đọng khi đối soát thu chi thủ công.
2. **Người chơi (Hội viên cố định & Khách vãng lai)**: Khảo sát thói quen tìm nhóm, độ tin cậy của thông tin sân bãi trên mạng xã hội, mức độ hài lòng về tính công bằng khi luân chuyển lượt đấu và tâm lý e ngại khi chuyển khoản tiền lẻ sau buổi chơi.

### 1.2. Mục tiêu cụ thể
- Lượng hóa tỷ lệ hủy hẹn giờ chót (bùng kèo) và mức độ thâm hụt tài chính của Host.
- Đánh giá thời gian Host bỏ ra để xếp cặp đấu bằng mắt thường và sự thiếu công bằng trong thời gian chờ.
- Đo lường thời gian thủ quỹ đối soát sao kê ngân hàng sau mỗi buổi chơi (thường từ 20–40 phút).
- Xác minh tỷ lệ người dùng mang "đa vai trò" (vừa là Host ở nhóm này, vừa là người chơi ở nhóm khác) để chứng minh tính tất yếu của mô hình **Phân quyền theo ngữ cảnh đa nhóm động (Dynamic Contextual Multi-Group RBAC)**.

---

## 2. KHẢO SÁT & SO SÁNH CÁC HỆ THỐNG TƯƠNG TỰ

| Tiêu chí phân tích | **Spond** (Châu Âu / Bắc Mỹ) | **Reclub** (Quốc tế) | **Công cụ truyền thống** (Zalo / FB) | **Hệ thống đề tài (BadmintonClub)** |
| :--- | :--- | :--- | :--- | :--- |
| **Đối tượng trọng tâm** | Đội nhóm thể thao nói chung | Nhóm thể thao phong trào | Hội nhóm tự phát tại VN | Hội nhóm cầu lông phong trào VN |
| **Biểu quyết giữ chỗ** | Có (đặt số lượng tối đa) | Có (bình chọn) | Bán thủ công (Tạo Poll bình chọn) | **Tự động khóa sổ + Hàng đợi thông minh** |
| **Chống lẻ sân giờ chót** | Không có | Không có | Đăng bài tìm người thủ công (dễ trôi bài) | **Phát radar tìm khách lân cận theo GPS** |
| **Điều phối xoay tua sân** | Không hỗ trợ | Không hỗ trợ (xếp tay) | Thủ công (bằng mắt thường hoặc bảng ghim) | **Tự động ưu tiên người nghỉ lâu + Cân bằng trình độ** |
| **Hạch toán vật tư tiêu hao**| Không có (thu phí cố định)| Không có | Thủ quỹ tự tính tay vào sổ | **Hạch toán số quả cầu bóc mới + Công thức đa hệ số** |
| **Đối soát ngân hàng** | Cổng quốc tế (Stripe) | Không có | Nhìn sao kê gạch nợ thủ công bằng mắt | **Mã VietQR nhúng tiền + Webhook tự động gạch nợ** |
| **Minh bạch tài chính** | Cơ bản | Không có | Chụp ảnh sao kê gửi vào nhóm chat | **Sổ quỹ mở thời gian thực cho toàn bộ hội viên** |
| **Kiến trúc đa nhóm** | Phân quyền đơn cấp | Phân quyền đơn cấp | Phân tán trên nhiều nhóm chat | **Đa nhóm động (Dynamic Multi-Group RBAC)** |

### Kết luận rút ra từ so sánh:
- Các giải pháp quốc tế (Spond, Reclub) tuy có giao diện hiện đại nhưng **hoàn toàn bỏ ngỏ các đặc thù sân cầu lông tại Việt Nam**: vật tư quả cầu lông bóc lẻ theo trận, luân chuyển sân đôi liên tục nhiều sân, và thanh toán chuyển khoản ngân hàng nội địa (VietQR).
- Ngược lại, nhóm chat truyền thống tuy phổ biến nhưng gây áp lực vận hành cực lớn, dễ gây sai sót và mất đoàn kết nội bộ do thiếu minh bạch.

---

## 3. KHẢO SÁT NGƯỜI DÙNG THỰC TẾ

### 3.1. Phương pháp & Mẫu khảo sát
- **Hình thức**: Khảo sát trực tuyến qua Google Form kết hợp phỏng vấn sâu 10 Host tại sân.
- **Đối tượng**: 50 người chơi cầu lông phong trào (Bao gồm hội viên cố định, khách vãng lai, Host và Thủ quỹ).
- **Thời gian**: Tháng 09/2026.

### 3.2. Kết quả khảo sát định lượng chính
1. **Cơ cấu vai trò đa nhóm**: **68%** người tham gia cho biết họ sinh hoạt ở từ 2 nhóm trở lên; trong đó **34%** vừa là người chơi ở nhóm này, vừa đang hoặc từng đứng ra tổ chức (Host/Thủ quỹ) ở nhóm khác ➔ *Chứng minh sự cấp thiết của tính năng chuyển đổi ngữ cảnh nhóm (`FR04 / UC05`)*.
2. **Sự cố khi chốt danh sách qua Poll nhóm chat**: **82%** người được hỏi từng gặp sự cố bùng kèo phút chót khiến sân bị lẻ người hoặc đến sân bị quá tải (1 sân 10–12 người).
3. **Tính công bằng khi xoay tua sân**: **74%** người chơi thừa nhận từng bị "bỏ quên", phải ngồi ngoài chờ liên tiếp 2–3 trận do người điều hành xếp theo cảm tính hoặc ưu tiên người quen.
4. **Nhu cầu tìm người giao lưu lân cận**: **88%** đánh giá điểm 4/5 và 5/5 về tính cần thiết của tính năng phát tín hiệu tìm tay vợt quanh tọa độ GPS khi phát sinh thiếu người.
5. **Phiền toái tài chính**: **78%** thủ quỹ mất từ 20 đến 40 phút sau buổi chơi để soi sao kê đối chiếu từng món tiền lẻ 40k–50k; **92%** người chơi mong muốn quét mã VietQR đúng số tiền để hệ thống tự động gạch nợ.

---

## 4. ĐẶC TẢ YÊU CẦU HỆ THỐNG

### 4.1. Danh mục 23 Yêu cầu Chức năng (Functional Requirements - FR)

#### Phân hệ 1: Quản trị Tài khoản & Đa nhóm (F1)
- **FR01**: Hệ thống cho phép người dùng đăng ký tài khoản định danh bằng số điện thoại/email hoặc liên kết bên thứ ba (Google/Apple ID).
- **FR02**: Hệ thống cho phép người dùng đăng nhập, duy trì phiên làm việc an toàn bằng JWT/Refresh Token và thực hiện đăng xuất khỏi thiết bị.
- **FR03**: Hệ thống cho phép một tài khoản người dùng tham gia đồng thời nhiều nhóm cầu lông khác nhau và đảm nhiệm các vai trò độc lập theo từng nhóm (Host ở nhóm A, Thủ quỹ ở nhóm B, Thành viên ở nhóm C).
- **FR04**: Hệ thống cho phép người dùng chuyển đổi nhóm làm việc trên giao diện ứng dụng di động; toàn bộ dữ liệu, quyền hạn và tính năng sẽ tự động đồng bộ theo vai trò của người dùng tại nhóm đang chọn tức thì dưới 300ms mà không cần đăng nhập lại.
- **FR05**: Hệ thống cho phép người dùng quản lý hồ sơ cá nhân: cập nhật ảnh đại diện, họ tên, giới tính, trạng thái nhận kèo giao lưu và trình độ chuyên môn tự đánh giá (thang 1-6: NB, Y, TBY, TB, TBK, K).

#### Phân hệ 2: Khám phá, Giữ chỗ & Kết nối Suất chơi (F2)
- **FR06**: Hệ thống cho phép người chơi tra cứu và tìm kiếm các nhóm cầu lông xung quanh vị trí hiện tại dựa trên tọa độ GPS và bán kính tùy chọn (công thức Haversine).
- **FR07**: Hệ thống cho phép người chơi xem chi tiết thông tin nhóm (địa chỉ sân sinh hoạt cố định, lịch chơi định kỳ trong tuần, trình độ mục tiêu và văn hóa sinh hoạt).
- **FR08**: Hệ thống cho phép thành viên thực hiện biểu quyết giữ chỗ trước thời hạn đóng sổ cho từng buổi sinh hoạt.
- **FR09**: Hệ thống tự động kích hoạt hàng đợi khi số lượng người đăng ký đạt ngưỡng trần tối đa theo số sân thực tế, đưa các lượt đăng ký tiếp theo vào danh sách chờ theo thứ tự thời gian.
- **FR10**: Hệ thống tự động đôn người chơi đứng đầu hàng đợi vào danh sách chính thức và gửi thông báo khi có thành viên khác báo vắng trước giờ khóa sổ.
- **FR11**: Hệ thống cho phép người quản trị (Host) phát tín hiệu tìm người giao lưu kèm định mức trình độ và chi phí buổi chơi khi phát sinh thiếu người đột xuất để chống lẻ sân.
- **FR12**: Hệ thống cho phép người chơi lân cận nhận thông báo đẩy tức thì, xem chi tiết và xác nhận tham gia trực tiếp vào suất chơi giao lưu còn trống của nhóm.

#### Phân hệ 3: Vận hành Buổi chơi & Điều phối tại sân (F3)
- **FR13**: Hệ thống cho phép người quản trị thiết lập thông tin nhóm, phân quyền vai trò cho thành viên khác trong nhóm và khởi tạo buổi sinh hoạt (khung giờ, số lượng sân, định mức người/sân, hạn chốt biểu quyết tham gia).
- **FR14**: Hệ thống cho phép người quản trị thực hiện điểm danh nhanh danh sách người chơi thực tế có mặt tại sân để nạp vào hàng đợi xoay tua.
- **FR15**: Hệ thống tự động vận hành giải thuật xoay tua sân: đề xuất luân chuyển 4 người chơi vào sân trống tiếp theo ưu tiên theo thời gian nghỉ lâu nhất (chống ngồi ngoài liên tiếp 2 trận) kết hợp cân bằng trình độ giữa hai cặp đấu.
- **FR16**: Hệ thống cho phép người quản trị can thiệp điều chỉnh danh sách thi đấu (hoán đổi người chơi, ghép lại cặp đấu) bằng thao tác kéo thả thủ công khi phát sinh nhu cầu riêng.
- **FR17**: Hệ thống cho phép người chơi theo dõi lịch thi đấu cá nhân theo thời gian thực (đang đánh ở sân nào, ai là bạn cùng chơi/đối thủ ở lượt kế tiếp và thời gian nghỉ dự kiến).

#### Phân hệ 4: Hạch toán Vật tư, Tài chính đa hệ số & Sổ quỹ mở (F4)
- **FR18**: Hệ thống cho phép trưởng nhóm (Host/Thủ quỹ) ghi nhận số lượng quả cầu lông thực tế đã bóc mới trong suốt buổi chơi và các khoản phụ phí phát sinh ngoài sân.
- **FR19**: Hệ thống tự động tính toán tổng chi phí và áp dụng công thức phân bổ đa hệ số dựa trên trọng số riêng biệt cho từng đối tượng (Nam, Nữ, Hội viên cố định đóng theo tháng, Khách vãng lai đóng theo buổi).
- **FR20**: Hệ thống tự động khởi tạo mã VietQR thanh toán (nhúng sẵn số tiền chính xác và mã định danh giao dịch duy nhất) cho từng cá nhân khi chốt sổ buổi chơi.
- **FR21**: Hệ thống tự động tiếp nhận thông báo biến động số dư qua Webhook ngân hàng theo thời gian thực để tự động khớp lệnh và gạch nợ sang trạng thái "Đã thanh toán" (`PAID`).
- **FR22**: Hệ thống cho phép ban điều hành nhóm và thành viên theo dõi Sổ quỹ mở nội bộ của nhóm (lịch sử thu chi, số dư khả dụng và danh sách người còn nợ phí theo thời gian thực).

#### Phân hệ 5: Đánh giá Tín nhiệm & Tương tác (F5)
- **FR23**: Hệ thống tự động tính toán, cập nhật và hiển thị Điểm uy tín hai chiều: tính Điểm tín nhiệm thành viên dựa trên mức độ chuyên cần, tỷ lệ bùng kèo và lịch sử đóng tiền; tính Điểm chất lượng nhóm dựa trên tính minh bạch sổ quỹ, mật độ sân thực tế và đánh giá từ người chơi.

---

### 4.2. Danh mục Yêu cầu Phi Chức Năng (Non-Functional Requirements - NFR)

1. **Bảo mật & Phân quyền Đa nhóm Theo Ngữ Cảnh Động (Dynamic Multi-Group RBAC Security)**:
   - Một tài khoản người dùng (`app_user`) có thể tham gia nhiều câu lạc bộ khác nhau với các vai trò và quyền hạn độc lập hoàn toàn. Hệ thống thẩm tra quyền hạn động theo từng Request dựa trên cặp `(UserId, GroupId)`.
   - Cơ chế bảo vệ: Tài khoản có quyền Host/Thủ quỹ ở nhóm A tuyệt đối không có quyền quản trị chéo sang nhóm B. Không sử dụng Global Query Filter ép cứng trên DbContext, đảm bảo người dùng tự do truy vấn danh sách mọi nhóm mình tham gia, tìm kiếm nhóm công khai và xem lịch sử thi đấu cá nhân xuyên suốt.
   - Mật khẩu người dùng được băm một chiều an toàn bằng thuật toán chuẩn BCrypt/Argon2. Kết nối API bắt buộc qua giao thức HTTPS và mã hóa bằng JWT.
2. **Độ tin cậy & Toàn vẹn giao dịch (ACID & Concurrency)**:
   - Tỷ lệ vượt quá sĩ số tối đa trên mỗi sân (khi nhiều người cùng bấm biểu quyết giữ chỗ tại giây cuối cùng) phải đạt **0%** nhờ cơ chế Khóa phân tán **Redis Distributed Lock**.
   - Đảm bảo tính toàn vẹn giao dịch tài chính: 100% các thao tác hạch toán chi phí hoặc cập nhật quỹ nhóm bị gián đoạn giữa chừng đều phải được tự động hoàn tác (`Rollback`).
   - Máy chủ API bảo đảm tính khả dụng tối thiểu **99.5%**.
3. **Hiệu năng & Thời gian phản hồi**:
   - Thời gian chuyển đổi giữa các nhóm trên ứng dụng di động phải diễn ra mượt mà dưới **300ms**.
   - Thời gian phản hồi của máy chủ đối với các truy vấn tìm kiếm nhóm theo tọa độ GPS và truy vấn lịch chơi phải đạt dưới **500ms**.
   - Thuật toán sắp xếp xoay tua sân trên bàn điều khiển phải thực thi và trả kết quả dưới **200ms**.
4. **Khả năng mở rộng & Dễ bảo trì**:
   - Mã nguồn Backend tuân thủ nghiêm ngặt **Clean Architecture** và nguyên lý phân tách **CQRS**, độc lập hoàn toàn giữa logic nghiệp vụ với cơ sở dữ liệu và các dịch vụ bên thứ ba.
