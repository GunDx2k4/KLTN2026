# CHƯƠNG I: TỔNG QUAN VỀ ĐỀ TÀI

> **Đề tài**: Xây dựng hệ thống hỗ trợ quản lý hội nhóm cầu lông phong trào  
> **Cơ sở đào tạo**: Trường Đại học Mở Hà Nội — Khoa Công nghệ Thông tin  
> **Giảng viên hướng dẫn**: TS. Lương Cao Đông  
> **Nhóm sinh viên thực hiện**:  
> - Nguyễn Xuân Dũng – 2210A04  
> - Nguyễn Công Hoan – 2210A04  
> - Bùi Văn Phúc – 2210A04  
> - Nguyễn Quốc Văn – 2210A02  
> **Thời gian thực hiện**: 2026  

---

## 1. GIỚI THIỆU ĐỀ TÀI
Trong kỷ nguyên số hóa, các hoạt động thể dục thể thao không chỉ dừng lại ở nhu cầu rèn luyện thể chất đơn thuần mà đã phát triển thành một nét văn hóa sinh hoạt cộng đồng đặc thù, đòi hỏi sự phối hợp nhịp nhàng giữa công tác tổ chức nội bộ và sự gắn kết giữa các thành viên.

Đề tài **“Xây dựng hệ thống hỗ trợ quản lý hội nhóm cầu lông phong trào”** được thực hiện nhằm mục đích số hóa và tinh gọn hóa toàn diện mô hình vận hành của các hội nhóm thể thao tự phát. Hệ thống không đơn thuần là một công cụ hỗ trợ điều phối sân đấu hay hạch toán chi phí tiêu hao vật tư, mà còn kiến tạo một **nền tảng lai kết hợp quản trị nội bộ và mạng lưới kết nối cộng đồng**. Giải pháp giúp người chơi dễ dàng tìm kiếm hội nhóm phù hợp, kết nối giao lưu linh hoạt và duy trì môi trường sinh hoạt thể thao minh bạch, công bằng và văn minh.

---

## 2. LÝ DO CHỌN ĐỀ TÀI

### 2.1. Bối cảnh thực tế
Trong thời đại số, khi hầu hết các lĩnh vực dịch vụ đều đã được tối ưu hóa thông qua các nền tảng công nghệ, thì hoạt động tổ chức và vận hành tại các hội nhóm thể thao phong trào—đặc biệt là bộ môn **cầu lông**—vẫn còn mang tính thủ công và phân mảnh. Cầu lông hiện là một trong những môn thể thao phong trào có quy mô người chơi hàng đầu tại Việt Nam, đặc trưng bởi:
- Hình thức thi đấu chủ đạo là **đánh đôi** (đòi hỏi cố định 4 người/sân).
- Các buổi sinh hoạt định kỳ có quy mô từ **10 đến 30 người trên 2 đến 4 sân đồng thời**.

Mặc dù thị trường đã có các giải pháp phần mềm quản lý sân bãi cho chủ cơ sở (chủ sân) hoặc ứng dụng xoay sân quốc tế, nhưng các giải pháp này hoàn toàn bỏ ngỏ bài toán tự quản lý nội bộ và kết nối của các hội nhóm đi thuê sân. Hậu quả là toàn bộ quy trình vận hành—từ tuyển thành viên, điều phối xoay tua trên sân cho đến tính toán chi phí tiêu hao vật tư—vẫn phải dựa vào các công cụ rời rạc như nhóm chat mạng xã hội (Zalo, Messenger) và bảng tính thủ công.

### 2.2. Những khó khăn và điểm đau hiện tại (Pain Points)

#### a. Từ góc độ Người chơi (Hội viên cố định & Khách vãng lai):
1. **Khó khăn tìm kiếm nhóm chơi phù hợp**: Người mới chơi hoặc người chuyển nơi cư trú rất khó tiếp cận các nhóm tương thích về vị trí địa lý, khung giờ, chi phí và văn hóa sinh hoạt. Việc dò hỏi qua các bài đăng trôi nổi trên mạng xã hội thiếu cơ chế xác thực về độ uy tín cũng như mật độ người/sân thực tế (nhiều trường hợp đến nơi mới biết 1 sân có 10–12 người chờ).
2. **Bất cập trong điều phối xoay tua tại sân**: Việc sắp xếp lượt thi đấu bằng mắt thường hoặc ghi nhớ cảm tính của trưởng nhóm (Host) thường dẫn đến tình trạng thiếu công bằng: người quen được đánh liên tục, trong khi thành viên mới hoặc người rụt rè phải ngồi ngoài chờ liên tiếp 2–3 trận.
3. **Phiền toái trong khâu thanh toán**: Sau khi thi đấu mệt mỏi, người chơi phải chờ thủ quỹ tính tiền lẻ, tự mở app ngân hàng, gõ STK, nhập số tiền và nội dung chuyển khoản thủ công. Sai sót số tiền hoặc cú pháp dẫn đến việc đối soát kéo dài.

#### b. Từ góc độ Ban điều hành nhóm (Host & Thủ quỹ):
1. **Bất ổn định nhân sự và nguy cơ lẻ sân do hủy hẹn sát giờ**: Tình trạng thành viên hủy hẹn cận giờ khiến sân đôi bị lẻ người và gây lãng phí chi phí thuê sân cố định. Trưởng nhóm buộc phải tự bù tiền túi hoặc đăng bài tìm người trám chỗ gấp trong vô vọng.
2. **Gánh nặng hạch toán chi phí và vật tư tiêu hao**: Quả cầu lông là vật tư tiêu hao đắt đỏ và hao mòn nhanh. Việc kiểm soát số lượng quả/ống cầu thực tế kết hợp với công thức chia tiền đa hệ số (nam, nữ, hội viên cố định đóng tháng, khách vãng lai đóng buổi) khiến thủ quỹ mất 30–60 phút đối soát sao kê ngân hàng và chịu rủi ro nợ đọng kéo dài sau mỗi buổi chơi.
3. **Hạn chế của phần mềm hiện tại**: Các app đặt sân chỉ phục vụ chủ sân; các app xoay sân nước ngoài (Spond, Reclub) không hỗ trợ hạch toán quả cầu bóc lẻ và không có cổng thanh toán VietQR ngân hàng nội địa.

### 2.3. Sự cần thiết của hệ thống
Hệ thống mang lại 4 giá trị cốt lõi giải quyết triệt để các vấn đề trên:
1. **Kết nối và ghép nối thông minh hai chiều**: Giúp người chơi tìm kiếm nhóm quanh tọa độ GPS; hỗ trợ tính năng "Tìm người giao lưu" phát tín hiệu theo bán kính để lấp chỗ trống tức thì khi có người bận đột xuất.
2. **Chuẩn hóa điều phối thi đấu công bằng**: Giải thuật xoay tua tự động ưu tiên người có thời gian nghỉ lâu nhất, đảm bảo 100% không ai bị ngồi ngoài liên tiếp 2 trận, đồng thời hỗ trợ cân bằng trình độ giữa hai cặp đấu.
3. **Tự động hóa hạch toán và minh bạch tài chính**: Tự động áp dụng công thức chia tiền đa hệ số theo lượng cầu bóc mới thực tế; sinh mã VietQR nhúng sẵn số tiền; tiếp nhận Webhook ngân hàng để tự động xác nhận thanh toán và công khai Sổ quỹ mở.
4. **Xây dựng môi trường sinh hoạt văn minh**: Cơ chế đánh giá tín nhiệm hai chiều (Điểm uy tín thành viên & Điểm chất lượng hội nhóm) giúp giảm thiểu tối đa tình trạng hủy hẹn sát giờ và chậm trễ tài chính.

---

## 3. MỤC ĐÍCH & MỤC TIÊU NGHIÊN CỨU

### 3.1. Mục đích nghiên cứu
Mục đích của đề tài là xây dựng một hệ thống quản lý hội nhóm và kết nối cộng đồng toàn diện cho bộ môn cầu lông phong trào trên nền tảng **Mobile App (Flutter)** kết hợp **Backend API (.NET 10 Clean Architecture)**. Hệ thống số hóa trọn vẹn vòng đời một buổi sinh hoạt: từ chuẩn bị (giữ chỗ, hàng đợi, tìm khách lân cận), tổ chức tại sân (điểm danh, xoay sân tự động) đến kết thúc (hạch toán chi phí, VietQR, tự động xác nhận thanh toán và sổ quỹ mở).

### 3.2. Mục tiêu nghiên cứu cụ thể
1. **Về mặt lý thuyết**: Hệ thống hóa kiến trúc phần mềm **Clean Architecture**, nguyên lý **CQRS & MediatR**, mô hình phân quyền theo ngữ cảnh đa nhóm động **(Dynamic Contextual Multi-Group RBAC)** và thuật toán tối ưu hóa điều phối xoay tua sân.
2. **Về mặt phân tích & thiết kế**: Khảo sát thực tế 50 người dùng; xây dựng hoàn chỉnh 23 Yêu cầu chức năng (FR01-FR23), 20 Ca sử dụng (UC01-UC20); thiết kế CSDL quan hệ trên PostgreSQL đạt chuẩn 3NF với 22 bảng thực thể.
3. **Về mặt xây dựng hệ thống**: Lập trình hoàn chỉnh Backend API trên .NET 10 tuân thủ Clean Architecture; tích hợp Redis Lock chống vượt trần sĩ số, cổng Webhook VietQR (SePay) và thông báo đẩy Firebase Cloud Messaging (FCM).
4. **Về mặt kiểm thử & đánh giá**: Áp dụng mô hình chữ V (V-Model); thực thi kiểm thử kiến trúc tự động (NetArchTest) đạt 37/37 tests xanh, kiểm thử nghiệp vụ Application đạt 44/44 tests xanh.

---

## 4. ĐỐI TƯỢNG VÀ PHẠM VI NGHIÊN CỨU

### 4.1. Đối tượng nghiên cứu
- **Đối tượng nghiệp vụ**: Quy trình tổ chức buổi chơi, đăng ký giữ chỗ, thuật toán hàng đợi xoay tua tại sân, định mức tiêu hao quả cầu lông, công thức tài chính đa hệ số, đối soát ngân hàng tự động.
- **Đối tượng người dùng**: Ban điều hành nhóm (Host, Thủ quỹ, Phó nhóm) và Cộng đồng người chơi (Hội viên cố định, Khách vãng lai).
- **Đối tượng công nghệ**: .NET 10, C# 13, Entity Framework Core 10, PostgreSQL, Redis, MediatR, FluentValidation, Flutter.

### 4.2. Phạm vi nghiên cứu
- **Nhóm tính năng bắt buộc (Core)**:
  - Xác thực tài khoản (JWT, Refresh Token).
  - Quản trị đa nhóm & phân quyền động theo ngữ cảnh (Dynamic Contextual Multi-Group RBAC: 1 người dùng tham gia nhiều nhóm với nhiều vai trò độc lập: Host, Phó nhóm, Thủ quỹ, Thành viên, Khách).
  - Khám phá nhóm quanh tọa độ GPS; đăng ký giữ chỗ có hàng đợi tự động.
  - Vận hành tại sân: Điểm danh, thuật toán xoay tua sân công bằng theo thời gian nghỉ.
  - Hạch toán chi phí đa hệ số theo quả cầu bóc mới; sinh mã VietQR thanh toán; tự động xác nhận thanh toán qua Webhook ngân hàng; Sổ quỹ mở.
- **Nhóm tính năng quan trọng**:
  - Tìm người giao lưu lân cận khi lẻ sân (quét radar theo bán kính km).
  - Đánh giá tín nhiệm hai chiều (Điểm thành viên & Điểm CLB).
- **Phạm vi ngoài đề tài (Future Work)**:
  - Phân hệ Super Admin quản trị toàn sàn; tích hợp ví điện tử trả trước; bán lẻ dụng cụ cầu lông.

---

## 5. PHƯƠNG PHÁP NGHIÊN CỨU
1. **Phương pháp nghiên cứu tài liệu**: Nghiên cứu lý thuyết Clean Architecture của Robert C. Martin, tài liệu Microsoft .NET 10, tài liệu PostgreSQL và tài liệu tích hợp cổng thanh toán VietQR.
2. **Phương pháp khảo sát thực tế**: Thu thập số liệu qua Google Form từ 50 người chơi và ban điều hành nhóm; phỏng vấn trực tiếp các Host tại các cụm sân phong trào tại TP.HCM và Hà Nội.
3. **Phương pháp phát triển & kiểm thử**: Áp dụng mô hình Agile/Scrum kết hợp mô hình kiểm thử V-Model song song qua từng giai đoạn phân tích, thiết kế và cài đặt.
