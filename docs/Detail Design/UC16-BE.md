# UC16 — Bàn giao tích hợp FE

Phạm vi: chốt sổ buổi chơi, xem trước phân bổ, điều chỉnh cá nhân và phê duyệt phát hành khoản thu. Hợp đồng API và các luồng dưới đây theo code BE hiện tại.

## 1. API và quyền

Base path: `/api/v1/sessions/{sessionId}/settlement`.

Gửi `Authorization: Bearer <accessToken>`, `X-Group-Id: <groupId của buổi>`; request có body dùng `Content-Type: application/json`. ID là UUID, thời gian trả về UTC; enum trả dạng chuỗi, riêng `gender` là số: **0=Khác, 1=Nam, 2=Nữ**. Response thành công là object trực tiếp, không bọc `data`.

| Method | Đường dẫn sau base path | Mục đích | Quyền |
|---|---|---|---|
| GET | `/form` | Chi phí, biểu phí và người có mặt | `FINANCE.FEE.MANAGE` |
| POST | `/preview` | Tính thử, trả bảng và token | `FINANCE.FEE.MANAGE` |
| POST | `/approve` | Phê duyệt, phát hành | `FINANCE.FEE.MANAGE` |
| GET | Không thêm | Đọc bảng khoản thu hiện tại | `FINANCE.FEE.VIEW` |
| POST | `/members/{userId}/pay` | Xác nhận đã thu tiền mặt, không có body | `FINANCE.FEE.MANAGE` |

Các API trên trả HTTP **200** khi thành công. Dựa vào quyền thực tế trong nhóm để hiện nút thao tác; BE vẫn kiểm tra quyền. Endpoint `pay` dành cho thao tác thu tiền tiếp nối, không phải nút “tôi đã chuyển khoản” của người chơi.

## 2. Luồng chốt sổ theo BE

Trước khi mở form, FE gọi GET settlement: UNPUBLISHED cho phép bắt đầu chốt; PUBLISHED hiển thị bảng đã phát hành và khóa sửa; LEGACY hiển thị khoản thu cũ, không phát hành lại. Chỉ hiện thao tác khi người dùng có quyền tương ứng.

### 2.1. Luồng chính

1. Host hoặc Thủ quỹ mở chức năng chốt sổ. BE xác thực đăng nhập và quyền quản lý khoản thu `FeeManage` trong nhóm.
2. BE tải biểu mẫu qua `GET /api/v1/sessions/{sessionId}/settlement/form`, gồm chi phí đang lưu, biểu phí, danh sách người đã điểm danh `CHECKED_IN`, giới tính, loại hội viên, hạn hội viên tháng và số người đã đăng ký nhưng chưa điểm danh.
3. Người quản lý nhập số quả cầu bóc mới, đơn giá mỗi quả cầu, tiền thuê sân thực tế và phụ phí.
4. Người quản lý gửi `POST /api/v1/sessions/{sessionId}/settlement/preview` để xem trước bảng phân bổ.
5. BE kiểm tra:
   - Buổi đã hết giờ thuê sân, chưa phát hành khoản thu, đang ở trạng thái cho phép chốt và không còn trận `PLAYING`.
   - Có người `CHECKED_IN`; dữ liệu tài khoản, giới tính, hội viên và biểu phí hợp lệ.
   - Số cầu không âm; các số tiền không âm, tối đa hai chữ số thập phân và không vượt giới hạn lưu trữ.
   - Nếu còn người `CONFIRMED` chưa điểm danh, người quản lý phải xác nhận xử lý vắng mặt.
6. BE tính tổng chi phí và phân bổ theo loại hội viên, biểu phí, giới tính, làm tròn và điều chỉnh cá nhân. Quy tắc cụ thể tại mục 5.
7. BE trả bảng phân bổ từng người, gồm loại giá, phần chi phí, phần quỹ tháng chi trả, giảm giá, số tiền trước/sau làm tròn, số tiền cuối cùng và các tổng chênh lệch. BE kèm token xem trước có hạn 15 phút; bước này chưa ghi chi phí hoặc tạo khoản thu.
8. Người quản lý kiểm tra bảng phân bổ và giữ nguyên nếu không cần điều chỉnh.
9. Người quản lý phê duyệt bằng `POST /api/v1/sessions/{sessionId}/settlement/approve`, gửi lại dữ liệu đã xem trước và token của chính người phê duyệt.
10. Khi `Settlement:WritesEnabled=true`, BE khóa quyết toán, kiểm tra token và dữ liệu nguồn, tính lại để xác nhận đúng kết quả đã xem trước, rồi lưu trong giao dịch:
    - Chi phí thực tế, bảng phân bổ và biểu phí tại thời điểm phát hành.
    - Một khoản thu cho mỗi người `CHECKED_IN`; số tiền dương có trạng thái `UNPAID`, số tiền bằng 0 có trạng thái `EXEMPT`.
    - Mã thanh toán định danh cho từng khoản thu; QR chưa được tạo trong bước UC16.
    - Thông tin điều chỉnh, người phê duyệt và thời điểm phát hành.
    - Chuyển buổi sang `CALCULATED`; những người còn `CONFIRMED` được chuyển sang `NO_SHOW` sau khi đã được xác nhận; các trận `WAITING` được bỏ qua.
    - Thông báo trong ứng dụng và bản ghi hàng đợi thông báo cho các khoản thu dương.
11. BE trả kết quả phát hành. Khi `Settlement:DispatchEnabled=true`, worker xử lý hàng đợi để gửi thông báo đẩy đến thiết bị đang hoạt động của người còn khoản thu `UNPAID` và số tiền dương. Gửi thông báo đẩy diễn ra sau phát hành, không chờ trong request phê duyệt.
12. Người quản lý xem kết quả bằng `GET /api/v1/sessions/{sessionId}/settlement` với quyền `FeeView`, rồi tiếp tục theo dõi thu tiền và đối soát ở UC17/UC18.

### 2.2. Luồng phụ / Ngoại lệ

#### 5a. Số liệu hoặc dữ liệu phân bổ không hợp lệ

1. BE từ chối xem trước hoặc phê duyệt nếu số cầu/số tiền âm, tiền sai độ chính xác, vượt giới hạn, thiếu biểu phí hoặc dữ liệu người chơi không hợp lệ.
2. Hội viên MONTHLY thiếu ngày hết hạn cũng bị từ chối; BE không tự suy đoán thời hạn.
3. Người quản lý sửa dữ liệu và gửi yêu cầu xem trước lại.
4. Nếu hủy trước khi phê duyệt, không tạo khoản thu, không phát hành thông báo. BE không có endpoint hủy bản xem trước; chỉ cần không gửi yêu cầu phê duyệt.

#### 5b. Buổi chưa đủ điều kiện chốt

1. BE từ chối nếu buổi chưa hết giờ, còn trận đang thi đấu hoặc trạng thái buổi không cho phép chốt.
2. Người quản lý chờ hết giờ hoặc hoàn tất xử lý trận trước khi xem trước lại.
3. Nếu còn người đã đăng ký nhưng chưa điểm danh, BE yêu cầu `ConfirmRemainingNoShows=true`.
4. Xem trước không đổi trạng thái điểm danh; chỉ khi phát hành thành công mới chuyển các booking `CONFIRMED` còn lại sang `NO_SHOW`. Những người này không được tính khoản thu trong UC16.

#### 8a. Điều chỉnh số tiền cá nhân

1. Người quản lý thêm điều chỉnh vào `Overrides`, gồm `UserId`, `FinalAmount` và `Reason`, rồi gửi yêu cầu xem trước mới.
2. BE chỉ chấp nhận người thuộc danh sách `CHECKED_IN`; không chấp nhận điều chỉnh trùng người, số tiền không hợp lệ hoặc lý do trống/quá 2.000 ký tự.
3. Số tiền điều chỉnh thay thế số tiền cuối cùng của người đó, không bị làm tròn lại và không tự chia phần chênh lệch sang những người khác.
4. BE trả bảng mới cùng token mới.
5. Người quản lý phê duyệt bằng dữ liệu và token mới; BE lưu số tiền, lý do, người và thời điểm điều chỉnh khi phát hành.

#### 9a. Token không hợp lệ, hết hạn hoặc dữ liệu thay đổi

1. Token sai chữ ký, sai người/nhóm/buổi hoặc không khớp dữ liệu gửi phê duyệt bị từ chối.
2. Với lần phát hành đầu tiên, token quá hạn 15 phút hoặc dữ liệu nguồn/kết quả đã thay đổi cũng bị từ chối.
3. Người quản lý xem trước lại, kiểm tra bảng mới rồi phê duyệt bằng token mới. Lần phê duyệt bị từ chối không phát hành khoản thu.

#### 10a. Gửi lại yêu cầu hoặc buổi đã có khoản thu

1. Gửi lại đúng yêu cầu đã phát hành, cùng token và cùng người phê duyệt: BE trả kết quả đã lưu, không tạo khoản thu hoặc thông báo trùng.
2. Dùng một token xem trước khác cho buổi đã phát hành: BE từ chối phát hành lại.
3. Buổi đã có khoản thu cũ cũng không được phát hành bộ khoản thu mới.
4. BE không cho sửa số tiền của khoản thu đã phát hành bằng luồng điều chỉnh trước phê duyệt.

#### 10b. Không thể phát hành

1. Nếu cờ phát hành đang tắt, không lấy được khóa quyết toán hoặc buổi đang được yêu cầu khác quyết toán, BE từ chối và trả lỗi tương ứng.
2. Nếu giao dịch phát hành lỗi, các thay đổi trong giao dịch được hoàn tác; không lưu dở bộ khoản thu.
3. Người quản lý xử lý nguyên nhân và thử lại; nếu dữ liệu hoặc thời hạn xem trước đã thay đổi thì phải xem trước lại.

#### 11a. Thông báo đẩy chưa gửi được

1. Nếu cờ gửi thông báo đang tắt, bản ghi hàng đợi được giữ lại để xử lý khi bật lại.
2. Nếu gửi thất bại, hệ thống ghi nhận lỗi và thử lại; đến ngưỡng 10 lần thất bại, bản ghi được đưa vào trạng thái dead-letter.
3. Khoản thu đã phát hành vẫn được giữ nguyên; lỗi gửi thông báo không tạo lại khoản thu.
4. Khoản thu bằng 0 không tạo thông báo khoản thu. Khi xử lý hàng đợi, khoản đã thanh toán hoặc không còn phù hợp sẽ không được gửi thông báo đòi thanh toán.

## 3. Request

POST `/preview`:

```json
{
  "shuttlecockUsed": 2,
  "shuttlecockPrice": 25000,
  "courtRentalCost": 300000,
  "extraExpense": 10000,
  "overrides": [],
  "confirmRemainingNoShows": false
}
```

- `shuttlecockUsed`: số nguyên không âm. Các trường tiền: số không âm, tối đa hai chữ số thập phân; BE giới hạn đến `9999999999999999.99` và kiểm tra cả tổng.
- Luôn gửi `overrides`, dùng `[]` khi không điều chỉnh. Mỗi phần tử: `{ userId, finalAmount, reason }`; người đó phải thuộc `participants`, không trùng ID, số tiền hợp lệ, lý do không trống và tối đa 2.000 ký tự. Cho phép điều chỉnh về 0.
- Chỉ đặt `confirmRemainingNoShows: true` sau khi người quản lý xác nhận những người chưa điểm danh là vắng mặt.

POST `/approve` gửi object:

```javascript
{
  input: inputDaGuiPreview,
  previewToken: responsePreview.previewToken
}
```

Token có hạn **15 phút**, gắn với người xem trước, nhóm, buổi, input và dữ liệu nguồn. Giữ nguyên token, không giải mã hay tự tạo. Người phê duyệt phải là người đã lấy token.

Nếu approve bị timeout/mất mạng: đọc lại GET settlement; khi cần gửi lại, dùng **đúng input/token cũ với cùng người dùng**. Retry đúng yêu cầu đã phát hành trả kết quả hiện tại, không tạo trùng; không tự lấy token mới để phát hành lại. Retry đúng yêu cầu đã thành công vẫn được chấp nhận sau hạn token.

## 4. Dữ liệu cần hiển thị

| Response | Trường chính |
|---|---|
| Form | `sessionId`, `shuttlecockUsed`, `shuttlecockPrice`, `courtRentalCost`, `extraExpense`, `rule`, `participants`, `remainingConfirmed` |
| Participant | `userId`, `fullName`, `gender`, `memberType`, `monthlyExpiryDate` |
| Preview | `allocation`, `previewToken`, `expiresAt` |
| Allocation | `sessionId`, `groupId`, `costs`, `rule`, `members` và các tổng bên dưới |
| Dòng preview | `userId`, `gender`, `memberType`, `monthlyExpiryDate`, `priceType`, `courtShare`, `shuttleShare`, `extraShare`, `monthlyFundCovered`, `baseAmount`, `discountApplied`, `rawAmount`, `calculatedAmount`, `finalAmount`, `adjustmentReason`, `roundingDelta`, `adjustmentDelta` |
| Bảng đã lưu (GET/approve/pay) | `sessionId`, `state`, chi phí, các tổng, `totalCollected`, `publishedAt`, `publishedBy`, `memberFees` |
| Dòng khoản thu | `feeId`, `userId`, `fullName`, `avatarUrl`, `priceType`, `finalFee`, `paymentStatus`, `paymentCode`, `qrCodePayload`, `paidAt`, `paymentMethod`, `breakdown` |

- Tên người ở preview lấy bằng cách ghép `members.userId` với `participants.userId`.
- `rule`: `id`, `feeMode` (`DYNAMIC`/`FIXED`), `dailyMemberFee`, `guestFee`, `femaleDiscountDaily`, `femaleDiscountGuest`, `roundRule`, `appliedAt`.
- Các tổng: `totalCost`, `totalReceivable`, `monthlyFundCovered`, `totalDiscount`, `policyDelta`, `roundingDelta`, `adjustmentDelta`, `balanceDifference`; preview còn có `shuttleCost`. Chênh lệch có thể âm. `totalCollected` chỉ có ở bảng đã lưu, là tổng khoản đang `PAID`.
- `finalAmount` ở preview tương ứng `finalFee` sau phát hành. `breakdown` là chi tiết phân bổ đã chốt; khoản cũ có thể không có breakdown.
- Trạng thái khoản thu: `UNPAID` = chờ thanh toán, `PAID` = đã thu, `EXEMPT` = miễn thu/số tiền 0. Không hiện nút thu tiền cho `EXEMPT`. `paymentMethod` có thể null, `CASH` hoặc `BANK_TRANSFER`.
- Trước phát hành hoặc với dữ liệu cũ, các trường tổng/snapshot nullable phải hiển thị “chưa có”; không ép null thành số 0.
- UC16 tạo `paymentCode`, chưa tạo QR; `qrCodePayload` rỗng là hợp lệ. Thanh toán chuyển khoản/QR theo UC17, đối soát theo UC18.

Model đầy đủ: [Requests](../../src/LegendsTeamVN.BadmintonClub.Application/DTOs/Settlement/Requests), [Responses](../../src/LegendsTeamVN.BadmintonClub.Application/DTOs/Settlement/Responses), [chi tiết allocation](../../src/LegendsTeamVN.BadmintonClub.Domain/Services/SettlementAllocationEngine.cs).

## 5. Quy tắc tính tiền hiện tại

FE hiển thị kết quả BE, không tự tính lại số tiền để phát hành:

- Hội viên tháng còn hạn tại lúc bắt đầu buổi: quỹ tháng chi trả cả sân, cầu và phụ phí; mặc định thu 0. Hết hạn tính như hội viên theo buổi. Thiếu hạn tháng thì bị chặn.
- `DYNAMIC`: chia đều từng thành phần chi phí thực tế cho người có mặt. `FIXED`: thu theo giá hội viên theo buổi/khách trong biểu phí.
- Nữ theo buổi/khách được trừ **mức tiền** cấu hình tương ứng, không nhân hệ số. Làm tròn lên theo `roundRule`.
- Override thay thế số tiền cuối cùng, không làm tròn lại và không phân bổ lại phần chênh lệch sang người khác. Dùng các tổng chênh lệch BE trả để giải trình.

## 6. Xử lý lỗi

Lỗi nghiệp vụ trả Problem Details có `status`, `code`, `detail` (có thể kèm `traceId`). Lỗi validation có thể dùng `errors`; lỗi xác thực/phân quyền hoặc JSON sai có thể không có `code`. FE xử lý cả HTTP status và nội dung, không chỉ dựa vào chuỗi thông báo.

| HTTP / code | FE xử lý |
|---|---|
| 400: validation, `Settlement.InvalidCosts`, `InvalidAdjustment`, `AmountOutOfRange` | Hiện lỗi ở form; sửa rồi preview lại |
| 400: `Settlement.PolicyRequired`, `MonthlyExpiryRequired`, `InvalidRule`, `InvalidUser`, `InvalidMembership` | Thông báo cần hoàn thiện biểu phí/hồ sơ/hội viên; không tự bỏ qua |
| 400: `Settlement.InvalidPreviewToken` | Bỏ token đang dùng, preview và kiểm tra lại trước khi duyệt |
| 401 / 403 | Đăng nhập lại hoặc báo thiếu quyền/sai nhóm; `Settlement.PreviewActorMismatch` yêu cầu đúng người xem trước |
| 404 | Buổi hoặc khoản thu không còn tồn tại; tải lại danh sách |
| 409: `Settlement.NotEnded`, `Session.ActiveMatches`, `Session.InvalidTransition` | Chưa đủ điều kiện chốt; xử lý buổi/trận rồi tải lại |
| 409: `Attendance.NoShowConfirmationRequired` | Hiện xác nhận vắng mặt, sau đó preview lại với cờ true |
| 409: `Settlement.PreviewExpired`, `PreviewStale` | Preview lại, hiển thị bảng mới và yêu cầu người dùng duyệt lại |
| 409: `Settlement.AlreadyPublished`, `LegacyFees` | GET settlement, chuyển sang bảng đã lưu; không tiếp tục phát hành |
| 409: `Settlement.Busy`, `LockUnavailable`, `PaymentCodeConflict` | Báo chưa phát hành được; tải lại trạng thái và cho thử lại có kiểm soát |
| 409: `Settlement.WritesDisabled` | Báo chức năng phát hành đang tạm dừng |
| 409: `Settlement.Exempt` | Khoản miễn thu không cần xác nhận thu tiền |
| 500 | Báo lỗi hệ thống, giữ dữ liệu nhập và mã truy vết nếu có; không coi là đã phát hành thành công |

Trong bảng, code viết tắt như `PreviewStale` vẫn mang tiền tố `Settlement.`.

## 7. Thông báo và chạy local

Thông báo trong ứng dụng/hàng đợi được tạo cùng khoản thu dương. Push gửi sau khi phát hành, tùy thiết bị và cấu hình; không chờ push để báo phê duyệt thành công. Sau thanh toán, tải lại GET settlement để cập nhật trạng thái và tổng đã thu.

Local API mặc định `http://127.0.0.1:54796`. Development trên máy đã cấu hình User Secrets cho UC16. Máy khác cần BE cấp `Settlement:PreviewSigningKey` riêng tối thiểu 32 byte, `WritesEnabled=true`; `DispatchEnabled=false` nếu chỉ test REST. Các cờ/khóa này thuộc server, không đặt trong FE. Database cần migration `AddSettlementPublicationSnapshot` và Redis hoạt động.

Collection dùng chung ở workspace `postman/UC16/UC16.postman_collection.json`: import, chọn No environment, nhập mật khẩu ở Variables rồi Run từ đầu. Collection tạo buổi và khoản thu test; không chạy trên dữ liệu thật.
