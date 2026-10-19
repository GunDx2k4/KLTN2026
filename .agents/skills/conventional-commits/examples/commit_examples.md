# Conventional Commits Examples (LegendsTeamVN.BadmintonClub)

## 1. Single Subsystem Commits (Atomic)

### Club & Membership
```text
feat(club): thêm api duyệt yêu cầu xin gia nhập câu lạc bộ

Bổ sung ApproveJoinRequestCommand, validator, handler và endpoint PUT.
Cập nhật trạng thái JoinRequest sang APPROVED và thêm bản ghi GroupMember.
```

### Session & Court Rotation
```text
feat(session): triển khai giải thuật xoay tua 4 người vào sân FR15

Tự động chọn 4 người chơi có thời gian nghỉ lâu nhất và ghép cặp có
chênh lệch điểm kỹ năng nhỏ nhất theo đặc tả UC13.
```

### Finance & Payments
```text
feat(finance): tích hợp webhook sepay tự động gạch nợ hóa đơn FR21

Bổ sung ProcessSepayWebhookCommand với khóa phân tán Redis và
kiểm tra tính idempotency dựa trên reference_code ngân hàng.
```

### Bug Fixes
```text
fix(auth): sửa lỗi refresh token bị hết hạn trước thời hạn cấu hình

Điều chỉnh thời gian hết hạn của RefreshTokenEntity sang UtcNow + 7 ngày.
```

### Architecture Tests
```text
test(arch): bổ sung kiểm tra bắt buộc parameterless constructor entity

Đảm bảo 100% Entity trong tầng Domain có constructor protected.
```

## 2. Multi-Subsystem Splitting Strategy

Khi một đợt thay đổi trải dài trên nhiều phân hệ không liên quan, không gom thành 1 commit:
- Commit 1: `feat(db): tạo migration bảng session_fee và group_fee_rule`
- Commit 2: `feat(finance): thêm use case tính tiền buổi chơi đa yếu tố`
- Commit 3: `docs(api): cập nhật danh mục tiến độ endpoint FR19`
