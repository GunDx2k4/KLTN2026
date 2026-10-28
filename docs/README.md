#  BỘ TÀI LIỆU KỸ THUẬT & BÁO CÁO KHÓA LUẬN TỐT NGHIỆP
## HỆ THỐNG HỖ TRỢ QUẢN LÝ HỘI NHÓM CẦU LÔNG PHONG TRÀO

> **Đơn vị đào tạo**: Trường Đại học Mở Hà Nội — Khoa Công nghệ Thông tin  
> **Giảng viên hướng dẫn**: TS. Lương Cao Đông  
> **Nhóm sinh viên thực hiện**:  
> - Nguyễn Xuân Dũng – 2210A04  
> - Nguyễn Công Hoan – 2210A04  
> - Bùi Văn Phúc – 2210A04  
> - Nguyễn Quốc Văn – 2210A02  
> **Nền tảng công nghệ**: ASP.NET Core (.NET 10), PostgreSQL, Clean Architecture, CQRS & MediatR, Redis, Flutter  
> **Năm thực hiện**: 2026  

---

##  DANH MỤC HỆ THỐNG TÀI LIỆU TOÀN DỰ ÁN

Toàn bộ báo cáo khóa luận và tài liệu kỹ thuật của dự án được biên soạn và phân rã thành các tập tài liệu chuẩn hóa trong thư mục `docs/`:

```
docs/
├── README.md                                 # [Trang chủ] Tổng quan, mục lục và hướng dẫn điều hướng
├── 01_TONG_QUAN_DE_TAI.md                     # [Chương I] Bối cảnh, mục tiêu, đối tượng & phạm vi nghiên cứu
├── 02_KHAO_SAT_VA_YEU_CAU.md                 # [Chương II] Khảo sát hiện trạng, so sánh giải pháp & 23 FRs/NFRs
├── 03_PHAN_TICH_HE_THONG_UC.md               # [Chương III] Phân tích chức năng F1-F5, DFD & 20 Ca sử dụng UC01-UC20
├── 04_THIET_KE_CO_SO_DU_LIEU_3NF.md          # [Chương IV.1] Thiết kế CSDL chuẩn hóa 3NF 22 bảng thực thể & ERD
├── 05_KIEN_TRUC_HE_THONG_VA_MA_NGUON.md      # [Chương IV.2-3] Kiến trúc phân tán, Multi-Tenancy & Clean Architecture
├── 06_QUY_TRINH_KIEM_THU_VMODEL.md           # [Chương V] Quy trình kiểm thử V-Model, 80 bài Tests & kịch bản UAT
├── API_DOCUMENTATION.md                      # [Kỹ thuật] Đặc tả chi tiết các RESTful APIs & cURL mẫu
├── DANH_SACH_VA_TIEN_DO_CAC_FR.md            # [Tiến độ] Ma trận trạng thái cài đặt 23 Yêu cầu chức năng
└── DEVELOPMENT_GUIDE.md                      # [Kỹ thuật] Hướng dẫn cài đặt môi trường, chạy migrations & seeding
```

---

##  TÓM TẮT NỘI DUNG TỪNG CHƯƠNG

###  [Chương I: Tổng Quan Về Đề Tài](01_TONG_QUAN_DE_TAI.md)
- **Bối cảnh thực tế**: Đánh giá sự bùng nổ của bộ môn cầu lông phong trào tại Việt Nam và những điểm đau nhức nhối khi quản lý thủ công qua nhóm chat Zalo/Facebook.
- **Mục đích & Mục tiêu**: Số hóa toàn diện vòng đời sinh hoạt của hội nhóm, giải quyết triệt để 4 bài toán: Giữ chỗ - Xoay tua sân - Hạch toán cầu bóc mới - Tự động xác nhận thanh toán qua VietQR.
- **Đối tượng & Phạm vi nghiên cứu**: Giới hạn phân hệ nghiệp vụ, đối tượng người dùng (Host, Thủ quỹ, Thành viên, Khách vãng lai) và công nghệ sử dụng.

###  [Chương II: Khảo Sát Hiện Trạng & Đặc Tả Yêu Cầu](02_KHAO_SAT_VA_YEU_CAU.md)
- **Khảo sát giải pháp tương tự**: Đặt lên bàn cân so sánh chi tiết giữa **Spond** (Mỹ), **Reclub** (Quốc tế), **Nhóm chat truyền thống** và **Hệ thống đề tài**.
- **Khảo sát người dùng thực tế**: Phân tích số liệu từ 50 mẫu khảo sát (68% người dùng sinh hoạt đa nhóm, 82% từng gặp sự cố hủy hẹn sát giờ gây lẻ sân, 78% thủ quỹ gặp khó khăn trong khâu đối soát sao kê).
- **Đặc tả yêu cầu**:
  - **23 Yêu cầu chức năng (FR01 → FR23)** chia theo 5 phân hệ cốt lõi.
  - **Các yêu cầu phi chức năng (NFR)**: Phân quyền theo ngữ cảnh đa nhóm động (Dynamic Contextual Multi-Group RBAC), Khóa phân tán chống vượt trần sĩ số (Redis Lock), Hiệu năng phản hồi < 300ms.

###  [Chương III: Phân Tích Hệ Thống & Đặc Tả 20 Ca Sử Dụng](03_PHAN_TICH_HE_THONG_UC.md)
- **Phân rã chức năng**: Ma trận 5 phân hệ lớn (F1 đến F5) thành 23 chức năng con chi tiết.
- **Sơ đồ luồng dữ liệu (DFD)**: Sơ đồ mức ngữ cảnh (Context Diagram) tương tác giữa Người chơi, Host, Cổng ngân hàng SePay và Bản đồ.
- **Đặc tả chi tiết 20 Ca sử dụng (UC01 → UC20)**:
  - Cung cấp đầy đủ: Tác nhân, Tiền điều kiện, Hậu điều kiện, Luồng sự kiện chính, Luồng rẽ nhánh, Luồng ngoại lệ và Quy tắc nghiệp vụ.
  - Đặc biệt làm rõ: **UC04** (Khởi tạo CLB), **UC05** (Chuyển đổi ngữ cảnh nhóm), **UC09** (Giữ chỗ & Hàng đợi), **UC13** (Xoay tua sân tự động theo giờ nghỉ), **UC16** (Chốt sổ chi phí đa hệ số), **UC18** (Đối soát Webhook tự động xác nhận thanh toán).

###  [Chương IV.1: Thiết Kế Cơ Sở Dữ Liệu Chuẩn Hóa 3NF](04_THIET_KE_CO_SO_DU_LIEU_3NF.md)
- **Mô hình thực thể**: Sơ đồ ERD quan hệ giữa 22 bảng thực thể.
- **Đặc tả 22 Bảng CSDL**: Chi tiết từng cột, kiểu dữ liệu, khóa chính (UUID), khóa ngoại, ràng buộc duy nhất, giá trị mặc định và ý nghĩa nghiệp vụ.
- **Ánh xạ 1:1**: Bảng đối chiếu giữa các bảng PostgreSQL và các class C# Entity Framework Core 10.

###  [Chương IV.2 & IV.3: Kiến Trúc Hệ Thống & Mã Nguồn Clean Architecture](05_KIEN_TRUC_HE_THONG_VA_MA_NGUON.md)
- **Mô hình Client-Server phân tán**: Giao tiếp giữa Mobile Flutter và Backend .NET 10 qua RESTful JSON APIs và SignalR WebSocket.
- **Cơ chế Phân quyền Đa Nhóm Động (Dynamic Contextual Multi-Group RBAC)**: Một tài khoản người dùng tham gia nhiều CLB với các vai trò độc lập, thẩm tra quyền động theo cặp `(UserId, GroupId)`, tối ưu qua Redis Cache.
- **Kiến trúc Clean Architecture 4 tầng**:
  - `Domain`: Trái tim nghiệp vụ thuần khiết theo chuẩn **Rich Domain Model** (CẤM Anemic Domain Model).
  - `Application`: Bộ điều phối ca sử dụng thuần túy theo **CQRS & MediatR Pipeline**, kiểm soát giao dịch qua `UnitOfWorkBehavior`.
  - `Presentation`: Cổng Minimal APIs chuẩn hóa mã lỗi **RFC 7807 ProblemDetails**.
  - `Persistence` & `Infrastructure`: Kết nối PostgreSQL 16+, Redis Lock, Webhook SePay, FCM.

###  [Chương V: Quy Trình Kiểm Thử Hệ Thống (V-Model)](06_QUY_TRINH_KIEM_THU_VMODEL.md)
- **Chiến lược QA/QC**: Áp dụng mô hình chữ V song song từ khâu phân tích, thiết kế đến cài đặt.
- **Kiểm thử tự động hoàn tất**:
  - **37 / 37 Architecture Tests (NetArchTest)**: Bảo đảm 100% không vi phạm quy tắc phân tầng.
  - **44 / 44 Application Tests**: Kiểm thử toàn diện logic nghiệp vụ, Aggregate Roots, đa nhóm và Handlers.
- **Kiểm thử hệ thống & Tải**: Thử nghiệm 20 người tranh chấp slot cuối cùng qua Redis Lock (tỷ lệ lỗi vượt trần 0%).
- **Kịch bản UAT**: 6 kịch bản nghiệm thu người dùng thực tế trên ứng dụng di động.

---

## ️ HƯỚNG DẪN THỰC THI & KIỂM THỬ DỰ ÁN

### 1. Kiểm tra toàn bộ bài kiểm thử kiến trúc (Architecture Tests)
```bash
dotnet test tests/LegendsTeamVN.BadmintonClub.Architecture.Tests/LegendsTeamVN.BadmintonClub.Architecture.Tests.csproj --verbosity minimal
```
*(Kết quả: 37 Passed, 0 Failed)*

### 2. Kiểm tra toàn bộ bài kiểm thử nghiệp vụ (Application Tests)
```bash
dotnet test tests/LegendsTeamVN.BadmintonClub.Application.Tests/LegendsTeamVN.BadmintonClub.Application.Tests.csproj --verbosity minimal
```
*(Kết quả: 44 Passed, 0 Failed)*

### 3. Chạy máy chủ API Backend
```bash
dotnet run --project src/Hosts/LegendsTeamVN.BadmintonClub.API
```
Truy cập Swagger UI tại: `https://localhost:7001/swagger` (hoặc cổng cấu hình tương ứng).
