# UC20 — Báo cáo kiểm thử BE sau khi bỏ cờ

Ngày kiểm tra: 06/11/2026. Phạm vi: source BE UC20, HTTP/JWT, PostgreSQL, worker/outbox, SignalR publisher và hồi quy các lớp liên quan. UC20 luôn hoạt động; EffectiveFromUtc vẫn giới hạn nguồn theo mốc UTC cố định.

## Phát hiện và sửa lỗi

Kiểm thử HTTP với PostgreSQL thật phát hiện response tạo review và replay cùng review có thể khác createdAt vài phần triệu giây. Nguyên nhân: DateTime có độ chính xác 100 ns, PostgreSQL lưu microsecond. Đã chuẩn hóa CreatedAt của review UC20 về microsecond trong GroupReview.CreateForSession. Kiểm tra vẫn so sánh toàn bộ response tạo mới/replay; không hạ assertion để né sai khác.

## Kiểm thử bổ sung

- HTTP dùng JWT và repository/PostgreSQL thật: card, entries, invitations, quality, POST review, replay cùng nội dung, 409 khi đổi nội dung, audit sai nhóm/thiếu quyền và audit hợp lệ theo permission thực trong DB.
- Sau POST, điểm nhóm đổi theo review, điểm cá nhân không đổi. Response không lộ email/comment trong card công khai.
- Engine finalize ghi Outbox thật; worker claim/gửi/complete, không claim LedgerUpdatedPush, không phát lại thông báo đã hoàn tất và không rebuild uy tín trong delivery.
- SignalR publisher gọi đúng UserId, tên ReviewInvitation/ReputationUpdated và payload; kiểm bằng IHubContext thay thế. Không coi đây là kết nối thiết bị/transport SignalR thật.
- Giữ các ca readiness, CASH/ledger, miễn thu, hủy/no-show, replay, rebuild xuyên nhóm, buổi đến muộn, snapshot quyền, concurrency, fault Outbox, migration Up/Down và stale writer của các wave trước.

## Môi trường và giới hạn

PostgreSQL localhost:55440/uc20_test; fixture tạo/xóa database uc20_test_<guid> riêng. Redis sandbox localhost:56386/56388/56389 cho khóa UC16/18/19; không dùng DB ứng dụng hoặc connection trong appsettings.

FCM dùng dịch vụ thay thế trong kiểm thử để kiểm payload, quyền, retry và người nhận; chưa gửi token thiết bị thật. Chưa gọi SePay thật, chưa áp migration vào DB chung. Các ca hạ tầng Application của phân hệ khác bị skipped không được tính là đạt UC20 hay đạt toàn bộ dự án.

## Kết quả cuối

| Bộ kiểm tra | Kết quả |
|---|---|
| UC20 Features.Reputation | 36 passed, 0 failed, 0 skipped |
| Application hồi quy rộng | 244 passed, 139 skipped, 0 failed; sau sửa timestamp, trước bổ sung assertion audit có quyền trong ca HTTP DB đã chạy lại ở bộ UC20 cuối |
| Domain UnitTests | 360 passed, 0 failed/skipped |
| Infrastructure với Redis sandbox | 89 passed, 0 failed/skipped |
| verify_architecture.py | 37/37 passed |
| Build toàn solution --no-restore | 0 lỗi; 1 cảnh báo NU1510 package Infrastructure đã có |
| git diff --check | Đạt |

Kết luận: kiểm chứng BE tự động trong phạm vi UC20 đạt, gồm HTTP và persistence/delivery trên sandbox. Không còn lỗi UC20 được phát hiện trong các kiểm tra đã chạy. Nghiệm thu nhà cung cấp, thiết bị/FE và migration DB triển khai vẫn là các bước ngoài bằng chứng kiểm thử này.
