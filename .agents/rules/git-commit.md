---
trigger: model_decision
description: "Quy ước tạo git commit message chuẩn Conventional Commits với mô tả Tiếng Việt có dấu đầy đủ, chuẩn UTF-8."
---

# 📝 Quy Ước Commit Code Chuẩn Hiện Đại (Conventional Commits)

Quy tắc này bắt buộc áp dụng cho toàn bộ kỹ sư phần mềm và AI Agent khi thực hiện tạo commit trong dự án **LegendsTeamVN.BadmintonClub**. Việc tuân thủ giúp lịch sử git minh bạch, dễ theo dõi, hỗ trợ tạo changelog tự động và phân luồng CI/CD chính xác.

---

## 1. Cấu Trúc Chuẩn của Một Commit Message

```text
<type>(<scope>): <tiêu đề ngắn gọn mô tả thay đổi>

[Phần thân (body) - Tùy chọn: Giải thích chi tiết "tại sao" và "thay đổi cái gì"]

[Phần chân (footer) - Tùy chọn: Ghi chú BREAKING CHANGE hoặc link Issue/PR]
```

---

## 2. Danh Mục Các `<type>` Bắt Buộc

Chỉ được sử dụng các `type` sau đây:

| Type | Ý nghĩa & Khi nào sử dụng | Ví dụ (Tiếng Việt có dấu) |
| :--- | :--- | :--- |
| **`feat`** | Thêm một tính năng mới hoặc một use case mới cho người dùng. | `feat(club): thêm api duyệt yêu cầu gia nhập` |
| **`fix`** | Sửa lỗi (bug fix) trong logic hoặc giao diện API. | `fix(auth): sửa lỗi refresh token bị hết hạn trước thời hạn` |
| **`refactor`**| Tái cấu trúc mã nguồn (không thêm tính năng, không sửa lỗi, giữ nguyên hành vi). | `refactor(session): tối ưu truy vấn lấy danh sách buổi sinh hoạt` |
| **`perf`** | Cải thiện hiệu năng xử lý hoặc tối ưu tốc độ truy vấn cơ sở dữ liệu/Redis. | `perf(cache): lưu cache thông tin câu lạc bộ bằng redis` |
| **`test`** | Thêm mới hoặc chỉnh sửa các bài kiểm thử (Unit test, Architecture test). | `test(arch): bổ sung test ràng buộc domain không phụ thuộc layer ngoài` |
| **`docs`** | Thêm hoặc cập nhật tài liệu (README, API docs, docs kiến trúc). | `docs(api): bổ sung đặc tả api tạo vai trò tùy chỉnh của câu lạc bộ` |
| **`chore`** | Các công việc bảo trì, cập nhật thư viện, cấu hình build, seed dữ liệu. | `chore(seed): cập nhật danh sách quyền hạn mặc định` |
| **`style`** | Chỉnh sửa định dạng code, xóa using thừa, căn lề (không ảnh hưởng logic). | `style(endpoints): xóa using dư thừa và format lại mã nguồn` |
| **`ci`** | Cập nhật cấu hình CI/CD (GitHub Actions, Dockerfile, pipeline). | `ci(github): thêm bước chạy architecture tests khi mở pull request` |
| **`revert`**| Hoàn tác (revert) một commit trước đó bị lỗi. | `revert: hoàn tác commit tạo migration phân hệ tài chính` |

---

## 3. Danh Mục `<scope>` Khuyên Dùng Trong Dự Án

`scope` giúp xác định phạm vi phân hệ bị tác động, đặt trong dấu ngoặc đơn `(...)`:

* `club`: Quản lý hồ sơ câu lạc bộ, lịch sinh hoạt, thành viên, vai trò nhóm.
* `session`: Buổi sinh hoạt cầu lông, điểm danh, đặt sân, ghép trận.
* `finance`: Sổ quỹ, quản lý thu chi, hội phí, tích hợp tài khoản ngân hàng.
* `auth`: Đăng ký, đăng nhập, JWT, refresh token, xác thực người dùng.
* `notification`: Hệ thống thông báo đẩy (FCM, email).
* `db` / `migration`: Di trú cơ sở dữ liệu PostgreSQL, DbContext, Seeder.
* `arch`: Kiến trúc Clean Architecture, quy tắc phân tầng, NetArchTest.
* `deps`: Cập nhật các gói NuGet hoặc công cụ phát triển.

---

## 4. Các Quy Tắc Viết Tiêu Đề (Subject Rules)

1. **Ưu tiên Tiếng Việt có dấu đầy đủ**: Khuyến khích viết **Tiếng Việt có dấu chuẩn UTF-8**, câu từ tự nhiên, rõ nghĩa để toàn bộ thành viên trong nhóm đọc hiểu dễ dàng nhất.
2. **Ngắn gọn & Trực tiếp**: Không quá **72 ký tự** ở dòng tiêu đề.
3. **Chữ thường**: Bắt đầu bằng chữ viết thường ngay sau dấu hai chấm và khoảng trắng `: `.
4. **Không dấu chấm**: Tuyệt đối không đặt dấu chấm (`.`) ở cuối dòng tiêu đề.
5. **Breaking Change**: Nếu thay đổi làm thay đổi hợp đồng API cũ (breaking change):
   * Thêm dấu chấm than `!` ngay trước dấu hai chấm: `feat(api)!: thay đổi cấu trúc payload trả về của get club`
   * Hoặc ghi ở phần footer: `BREAKING CHANGE: Thay đổi toàn bộ trường Code thành ClubCode trong DTO`.

---

## 5. Bảng So Sánh Mẫu Commit TỐT vs XẤU

| ❌ Commit Xấu (CẤM) | ✅ Commit Tốt (CHUẨN TIẾNG VIỆT CÓ DẤU) | Lý do |
| :--- | :--- | :--- |
| `update code` | `feat(club): thêm api cập nhật ảnh đại diện câu lạc bộ` | Rõ ràng phân hệ và hành động |
| `fix bug` | `fix(session): sửa lỗi không lưu được điểm số trận đấu` | Biết chính xác sửa lỗi gì ở đâu |
| `WIP` | `feat(finance): tạo command lập phiếu thu quỹ nhóm` | Không commit mơ hồ, giữ commit có nghĩa |
| `sửa db và thêm api mới` | Chia thành 2 commit: `feat(db): ...` và `feat(api): ...` | Đảm bảo nguyên tắc Atomic Commit |
| `feat: update` | `refactor(persistence): tách cấu hình fluent api ra file riêng` | Mô tả chuẩn xác công việc tái cấu trúc |
