# Anpero.Payment.QrPayload — bản source được pin

Upstream: https://github.com/anpero-vietnam/Payment.QrPayload

Source commit của NuGet 1.0.1: `60b8f3fa7b94380f31470980398cf4c11f39acce`. License Unlicense giữ nguyên ở LICENSE. Hash source trước patch và Git blob Id tại upstream-manifest.json.

Bản build cục bộ `1.0.1-uc17.1`, assembly/file version `1.0.1.1`, không phải NuGet 1.0.1 nguyên gốc. Người dùng đã duyệt ngày 01/11/2026.

Thay đổi duy nhất trong C# upstream: Enums/PaymentService.cs, Napas247_Account Description từ `0208QRIBFTTA` thành `QRIBFTTA`. VnQrConfig tự bọc tag 02 và độ dài; không đổi encoder TLV/CRC, không sửa output sau encode. Nhánh Card và các dịch vụ khác không được sửa/không được adapter UC17 sử dụng.

Project file cục bộ chỉ đặt build/version/provenance; Infrastructure và POC tham chiếu cùng source qua ProjectReference để tránh build API dùng nhầm NuGet gốc. Không sửa cache NuGet, không phát hành package hoặc gửi mã nguồn ra upstream.
