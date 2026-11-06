# UC20 / FR23 — Hợp đồng backend và bàn giao Wave 1

Cập nhật 06/11/2026. Phạm vi source: Domain, bảo toàn lịch sử hủy, mapping và migration; chưa có worker/API/delivery UC20. Không tạo bảng mới. Chưa áp migration lên DB chung.

## 1. Policy và phạm vi nguồn

PolicyVersion `UC20-V1`, JSON `SchemaVersion=1`. Uy tín 0–100, baseline là điểm hiện hữu; đúng giờ trong 15 phút +1, đến trễ -3, NO_SHOW -20, hủy suất chính thức từ DeadlineBooking đến trước StartTime -10. Trả trong 24 giờ từ PublishedAt +1; ngân hàng trả muộn -3. CASH xác nhận muộn/EXEMPT/nguồn thiếu trung lập. Buổi có vi phạm chỉ cộng phạt. Không cộng/trừ theo số sao hoặc việc bỏ qua review.

Rebuild theo EndTime UTC rồi SessionId dạng UUID N với so sánh ordinal; clamp sau từng entry, bỏ thưởng vượt trần. Buổi đối soát muộn phải rebuild lịch sử đã có. Nhãn dưới 80, gỡ từ 80. Làm tròn decimal hai số lẻ AwayFromZero.

Chất lượng Q = 0.60 R + 0.25 T + 0.15 D. R trung bình review buổi; T = 1 + 4 × trung bình hai tỷ lệ hóa đơn và hội viên có LedgerView. Tử/mẫu số tổng hợp qua buổi, loại guest khỏi tập hội viên. D dùng số người CHECKED_IN/sân có trận FINISHED: ≤6 đạt 5, ≥12 đạt 1, khoảng giữa tuyến tính. Thiếu component thì chuẩn hóa trọng số còn lại; không có dữ liệu giữ điểm hiện hữu và Summary.Score=null.

## 2. Contract Domain cho Wave 2

- `SessionBooking.CancelWithEvidence`: append lịch sử cùng chuyển ABSENT, giữ deadline/quyền/hàng đợi. Hai handler hủy dùng TimeProvider. Tái đặt dùng booking cũ, không reset lịch sử.
- `BadmintonSession.CaptureReputationSnapshot`: cần nạp đầy đủ Bookings/Matches và truyền toàn bộ fee/ledger/bank liên quan (kể cả nguồn sai/xóa để phát hiện). Kiểm buổi hoàn tất, fee đúng tập CHECKED_IN, PAID/EXEMPT, ledger duy nhất, nguồn ngân hàng/CASH. Không dùng danh sách đã lọc chỉ còn nguồn hợp lệ để lách readiness.
- `SessionBooking.RecordReputation`: lưu một kết quả/bằng chứng/người/buổi; replay cùng bằng chứng không đổi version. Tập chấm gồm CHECKED_IN, NO_SHOW và hủy suất chính thức; trường hợp không có fee vẫn phải được chấm theo attendance.
- `BadmintonSession.FinalizeReputation`: recheck snapshot với nguồn fee/ledger/bank, yêu cầu các booking thuộc tập chấm đã có kết quả; chuyển Finalized và phát sự kiện một lần. Snapshot mới khác snapshot đã chốt bị từ chối.
- `AppUser.RebuildReputation` và `ClubGroup.RebuildQuality`: nhận toàn bộ nguồn hợp lệ từ repository, giữ baseline/coverage; không dùng object cũ của profile/nhóm để cập nhật.
- `GroupReview.CreateForSession`: eligibility đã chốt trên booking, Finalized, cửa sổ 7 ngày từ FinancialReconciledAt; rating nguyên 1–5, comment trim/null tối đa 2.000. Review buổi không qua UpdateReview.

Wave 2 chịu trách nhiệm transaction nguyên tử cho snapshot/booking/projection/Notification/Outbox và recheck dưới khóa; Domain một mình không chứng minh transaction/repository đã thực thi. Sự kiện Domain hiện được publish khi SaveChanges theo pipeline đang có, không chứng minh mạng đã gửi. Worker không tham gia transaction thu tiền UC18.

## 3. Mapping đã có trong source

Migration `20261105162124_AddSessionReputation`, baseline `20261105065216_TrackMonthlyMembershipPeriod`. Năm Configuration gọi helper ReputationModelConfiguration tương ứng; không thêm DbSet/entity table. Cột và constraint chi tiết tại phần mở rộng UC20 trong tài liệu schema.

JSONB dùng converter và ValueComparer serialize/clone để EF phát hiện thay đổi danh sách, không dùng owned collection tạo bảng. Trường filter/unique nằm ngoài JSONB. DB CHECK kiểu object/array, còn cấu trúc sâu do Domain kiểm; không có FK/unique cho phần tử JSONB.

ReputationWriteGuard tăng version khi ghi user/nhóm/buổi/booking, chặn sửa kết quả gốc đã chốt và sửa/xóa review UC20 qua SaveChanges. FK RESTRICT bảo vệ booking/review khi xóa nguồn cha; việc này cũng làm chặt hơn thao tác hard-delete dữ liệu cũ có booking/review. Soft-delete không tự loại điểm khỏi nguồn rebuild; Wave 2 phải truy vấn nguồn nội bộ đúng phạm vi.

ReputationCorrections hiện có cấu trúc lưu, chưa có public workflow correction. Không trực tiếp sửa JSON/điểm bằng SQL để sửa nghiệp vụ. Không có bảng lưu từng phiên bản tổng hợp chất lượng nhóm.

Up preflight chặn điểm cũ ngoài miền, không tự sửa dữ liệu. Down khóa các bảng nguồn và chặn nếu có dữ liệu UC20 (kể cả pending, lịch sử hủy, review, notification/outbox). Version có thể tăng do writer cũ, nên Down dựa vào dữ liệu nghiệp vụ chứ không xem mọi version khác 0 là UC20 đã xử lý.

## 4. API và quyền dự kiến, chưa triển khai

| API | Quyền |
|---|---|
| GET /api/v1/user/me/reviews/invitations | JWT, ownership/eligibility, gồm guest đã tham gia |
| POST /api/v1/user/me/reviews | SessionId/Rating/Comment; UserId từ JWT; review buổi duy nhất |
| GET /api/v1/reputation/players/{userId} | JWT; card tối thiểu score/nhãn/policy/thời điểm/count |
| GET /api/v1/reputation/me/entries | Nhật ký của chính mình từ booking, định danh BookingId |
| GET /api/v1/reputation/group-quality?groupId=... | JWT; tổng hợp không chứa tài chính/PII/comment riêng |
| GET /api/v1/clubs/{clubId}/sessions/{sessionId}/reputation-audit | GroupPermissions.Operations.SessionUpdate; tái kiểm session thuộc nhóm |

Comment chỉ tác giả và ban điều hành đúng nhóm xem. Guest không cần LedgerView để đánh giá; không tự cấp quyền tài chính. Client không gửi delta/score/UserId. Profile/Khám phá giữ field điểm hiện hành và ưu tiên GPS.

## 5. Kiểm chứng và giới hạn Wave 1

- Domain policy/readiness/review/lịch sử hủy có unit tests tại `tests/LegendsTeamVN.BadmintonClub.UnitTests/Domain/Reputation`.
- Mapping, JSON round-trip, SQL migration không CreateTable/DropTable, unique/FK và write guard có tests tại `tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Reputation`.
- PostgreSQL integration tests yêu cầu `UC20_TEST_POSTGRES` trỏ đúng localhost:55440/uc20_test. Fixture chỉ tạo/xóa DB ngẫu nhiên `uc20_test_<guid>`; không dùng connection ứng dụng.
- Máy hiện chưa có sandbox UC20 và Docker daemon chưa chạy: các test Up/Down thực, constraint và stale writer trên PostgreSQL được đánh dấu skipped, không coi là pass.
- Python alias không chạy được nên script verify_architecture.py chưa chạy; đã dùng trực tiếp project Architecture.Tests để kiểm cùng bộ NetArchTest. Không gọi đây là script pass.
- EF CLI 9.0.11 cảnh báo cũ hơn runtime 10.0.10 nhưng đã sinh migration; cần kiểm pending-model changes và SQL trước khi áp vào sandbox.

Chi tiết số test/build cuối cùng được ghi ở cuối lượt Wave 1 trong plan. Wave 2 và Wave 3 chưa thực hiện; không coi FR23 hoàn thành hoặc DB chung đã có schema mới.
