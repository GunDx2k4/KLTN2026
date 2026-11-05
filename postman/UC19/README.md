# UC19 — Gói tích hợp backend

Import UC19.postman_collection.json và UC19.sandbox.postman_environment.json. Có 17 request cho 5 GET/3 POST, lọc cá nhân, 401/403/400, replay, conflict và thiếu quỹ. Các request dùng token Bearer từ environment; tệp bàn giao để trống token, không kèm credential.

Đặt baseUrl đúng sandbox, clubId của nhóm thử, token của người có LedgerView/LedgerManage. Bật Ledger:Enabled trên API sandbox để chạy các GET; WritesEnabled chỉ cần khi chạy các POST. Mặc định runWrites=false: request ghi và download phụ thuộc dữ liệu ghi được bỏ qua. Không dùng collection ghi trên nhóm vận hành hoặc tiền/nợ thật.

Để chạy đủ kịch bản ghi trên sandbox đã chuẩn bị:

1. Bật Ledger:Enabled/WritesEnabled trên API sandbox; giữ dispatch/realtime tắt nếu chưa kiểm provider.
2. Chọn hội viên MONTHLY ACTIVE đủ kỳ, có MonthlyMembershipSince hợp lệ; điền memberId, contributionMonth kỳ hiện tại Việt Nam và expectedMonthlyAmount từ candidates/snapshot. Không dùng legacy thiếu bằng chứng để ép chạy.
3. Quỹ phải đủ expenseAmount; insufficientAmount phải lớn hơn số dư, trong numeric(18,2). Bộ mặc định dùng khoản thử 1.000 đồng và số thiếu quỹ 9999999999999999.99, cần kiểm điều kiện trước khi chạy.
4. Đặt runWrites=true. Chọn cùng một PDF/PNG/JPEG <= 5 MiB tại trường invoice trong cả request 13 và 14; request 17 cần hóa đơn này để trả 200. Nếu bỏ file, API vẫn cho tạo chi nhưng download sẽ 404, không dùng kỳ vọng 200 của kịch bản có file.
5. Chạy theo thứ tự. Script lưu monthlyContributionId và expenseLedgerId, sinh RequestId một lần để dùng lại khi retry. 12/14 phải trả replayed=true; 15 phải 409 do đổi nội dung, 16 phải 409 thiếu quỹ. Kiểm error code để phân biệt thiếu quỹ với RequiresReconciliation.
6. Dùng environment mới/nhóm fixture mới khi bắt đầu một kịch bản độc lập. Không đổi RequestId để lách xung đột hoặc thu trùng trên cùng dữ liệu.

Collection đã được kiểm JSON, biến và cú pháp JavaScript; các route, nghiệp vụ và Swagger được kiểm bằng HTTP/Kestrel với PostgreSQL/Redis trong test .NET. Không coi đó là đã chạy collection bằng Postman CLI. Không có request SePay trong gói này; tích hợp SePay để nghiệm thu sau khi có FE theo yêu cầu.
