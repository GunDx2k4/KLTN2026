---
name: implement-session-engine
description: >-
  Use this skill when implementing or modifying features related to Badminton Session operations, Check-in, Match Rotation Engine, and Live Court Tracking (FR14, FR15, FR16, FR17) in LegendsTeamVN.BadmintonClub.
---

# 🏸 Kỹ Năng Triển Khai Phân Hệ Vận Hành Buổi Chơi & Xoay Tua Sân (FR14 - FR17)

Skill này hướng dẫn Agent triển khai logic nghiệp vụ tại sân cho các ca sử dụng từ **UC12 đến UC15**:
- **FR14**: Điểm danh nhanh tại sân (`UC12`)
- **FR15**: Giải thuật tự động đề xuất 4 người vào sân ưu tiên thời gian nghỉ lâu nhất (`UC13`)
- **FR16**: Host can thiệp hoán đổi người chơi thủ công (`UC14`)
- **FR17**: Người chơi theo dõi lượt thi đấu realtime (`UC15`)

---

## 1. Domain Entities & Invariants Cần Tác Động

1. **`BadmintonSession`**:
   - Quản lý trạng thái buổi chơi: `OPEN` ➔ `LOCKED` ➔ `PLAYING` ➔ `CALCULATED` ➔ `COMPLETED`.
   - Thuộc tính: `CourtCount`, `MaxPlayers`, `ShuttlecockUsed`, `CourtRentalCost`.
2. **`SessionBooking`**:
   - Phương thức Domain: `CheckIn(DateTime checkinTime)` ➔ Cập nhật `Status = CHECKED_IN`, gán `CheckinAt = checkinTime`.
3. **`SessionMatch`**:
   - Trạng thái: `WAITING` ➔ `PLAYING` ➔ `FINISHED` (hoặc `SKIPPED`).
   - Thuộc tính: `CourtNumber`, `RoundNumber`, `TimeStart`, `TimeEnd`.
4. **`MatchPlayer`**:
   - Ghi nhận 4 người chơi trên sân: 2 người `SideTeam.TEAM_A` và 2 người `SideTeam.TEAM_B`.
   - Cờ `IsManualSwapped`: Đánh dấu `true` nếu có sự can thiệp thủ công từ Host.

---

## 2. Giải Thuật Đề Xuất Xoay Tua Sân (Match Rotation Engine - FR15)

Khi một sân hoàn thành trận đấu (`status = FINISHED`) hoặc có sân trống:

### 2.1. Bước 1: Lọc tập ứng viên hợp lệ
- Lấy toàn bộ người chơi trong buổi sinh hoạt thỏa mãn:
  - Đã điểm danh có mặt: `booking.Status == BookingStatus.CHECKED_IN`.
  - Không đang thi đấu trên bất kỳ sân nào khác (`match.Status != MatchStatus.PLAYING`).

### 2.2. Bước 2: Tính toán thời gian nghỉ ngơi (Rest Time)
Với mỗi ứng viên $P_i$, tính:
$$\text{RestTime}(P_i) = \text{CurrentTime} - \text{LastMatchEndTime}(P_i)$$
*(Nếu người chơi chưa đánh trận nào kể từ lúc điểm danh, ưu tiên tối đa: gán RestTime lớn nhất).*

Sắp xếp danh sách ứng viên giảm dần theo $\text{RestTime}$. Chọn ra **4 người có thời gian nghỉ lâu nhất** để đảm bảo quy tắc bất biến: **Không ai bị ngồi ngoài liên tiếp 2 trận**.

### 2.3. Bước 3: Ghép cặp cân bằng trình độ (Skill Balance)
Trong 4 người chơi được chọn $\{P_1, P_2, P_3, P_4\}$ với điểm trình độ tương ứng $\{S_1, S_2, S_3, S_4\}$ (lấy từ `app_user.skill_level`):
- Có 3 cách ghép cặp đôi:
  1. $(P_1, P_2)$ vs $(P_3, P_4)$ ➔ Chênh lệch: $|(S_1 + S_2) - (S_3 + S_4)|$
  2. $(P_1, P_3)$ vs $(P_2, P_4)$ ➔ Chênh lệch: $|(S_1 + S_3) - (S_2 + S_4)|$
  3. $(P_1, P_4)$ vs $(P_2, P_3)$ ➔ Chênh lệch: $|(S_1 + S_4) - (S_2 + S_3)|$
- Chọn phương án ghép cặp có **độ chênh lệch nhỏ nhất** để tạo ra trận đấu kịch tính và cân tài cân sức.

---

## 3. Quy Chuẩn Bắn Tín Hiệu Thời Gian Thực (SignalR WebSocket)

Khi Host bấm bắt đầu trận đấu hoặc hoán đổi người chơi:
1. Lưu bản ghi `session_match` và `match_player` vào CSDL qua `UnitOfWorkBehavior`.
2. Gửi sự kiện qua `IHubContext<SessionHub>` đến nhóm kết nối `Group_{sessionId}`:
   ```csharp
   await hubContext.Clients.Group($"Session_{sessionId}")
       .SendAsync("MatchStarted", new MatchLiveDto(...));
   ```
3. Toàn bộ điện thoại của người chơi tại sân tự động cập nhật bảng đấu mà không cần tải lại trang.
