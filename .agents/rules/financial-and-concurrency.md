---
trigger: glob
globs: "src/**/Finance/**, src/**/Session**, src/**/Entities/**Fee**, src/**/Entities/**Booking**"
description: "Redis distributed locks, decimal currency safety, VietQR generation, SePay webhook idempotency, and ACID ledger transactions."
---

# Quy Tac Xu Ly Tranh Chap Dong Thoi & Hach Toan Tai Chinh (Concurrency & Finance Rules)

> [!IMPORTANT]
> Dam bao tinh toan ven tuyet doi ve so du tien bac, ngan ngua triet de Race Condition (tranh chap slot gio chot) va xu ly doi soat ngan hang khong bao gio bi ghi dup (Idempotent Webhook Processing).

---

## 1. Quy Tac Khoa Phan Tan (Redis Distributed Lock)

### 1.1. Boi canh bat buoc su dung
1. **Bieu quyet giu slot buoi choi (`BookSlotCommand`):** Khi nhieu nguoi cung an giu slot tai giay cuoi, tranh truong hop tong so nguoi `CONFIRMED` vuot qua tran `MaxPlayers` cua cum san.
2. **Tiep nhan slot giao luu (`AcceptRecruitmentSlotCommand`):** Tranh 2 khach cung chiem 1 suat tuyen duy nhat con lai.
3. **Quyet toan chi phi buoi choi (`CalculateSessionFeeCommand`):** Tranh truong hop 2 Host cung bam tinh tien dong thoi gay tao 2 bo hoa don trung lap.
4. **Tiep nhan Webhook SePay (`ProcessSepayWebhookCommand`):** Ngan chan 2 request webhook cung xu ly mot ma giao dich ngan hang cung luc.

### 1.2. Chuan muc khoa phan tan:
- **Cau truc Key dinh danh:**
  - Dat cho buoi choi: `lock:session:{sessionId}:booking`
  - Dang ky suat tuyen: `lock:recruitment:{recruitmentId}:slot`
  - Quyet toan buoi choi: `lock:session:{sessionId}:settlement`
  - Webhook giao dich: `lock:sepay:transaction:{transactionId}`
- **Thoi gian cho lay khoa (Wait Time):** Toi da **3 giay**.
- **Thoi gian tu dong giai phong (Lease Time):** Toi da **5 giay** (chong Deadlock khi node bi crash).
- **Luong xu ly trong pham vi khoa:**
  1. Nap so luong nguoi da co ve `CONFIRMED`.
  2. Neu `count < maxPlayers` -> Gan `status = CONFIRMED`, `queue_order = 0`.
  3. Neu `count >= maxPlayers` -> Gan `status = WAITING`, `queue_order = maxOrder + 1`.
  4. Luu thay doi qua Repository va giai phong khoa.

---

## 2. Quy Tac Hach Toan Chi Phi Da Yeu To (FR19)

### 2.1. Kieu du lieu tien te bat buoc:
- **BAT BUOC DUNG `decimal`** cho toan bo cac thuoc tinh tien te: `decimal Amount`, `decimal UnitPrice`, `decimal DiscountPercentage`, `decimal RentalCost`.
- **CAM TUYET DOI** dung `float` hay `double` do dac tinh sai so bieu dien nhi phan lam lech so du tai chinh.

### 2.2. Cong thuc quyet toan buoi choi chuan:
$$\text{Tong chi phi buoi choi} = \text{Tien thue san co dinh} + (\text{So qua cau boc moi} \times \text{Don gia qua cau}) + \text{Phu phi phat sinh}$$

### 2.3. Phan bo theo loai hinh nguoi choi:
1. **Hoi vien goi thang (`MONTHLY`):**
   - Tien san va cau dinh ky duoc tru tu tien quy dong thang.
   - Khoan thu buoi cua hoi vien thang gan = `0d` hoac trang thai `EXEMPT` (tru khi co phu phi lien hoan dot xuat).
2. **Hoi vien ngay (`DAILY`):**
   - Dong theo muc `DailyMemberFee` hoac chia deu chi phi thuc te.
   - Nu: Tu dong tru di `FemaleDiscountDaily`.
3. **Khach vang lai (`GUEST`):**
   - Dong theo muc `GuestFee` (chi phi chia deu + phu phi vang lai).
4. **Quy tac lam tron tien (`RoundRule`):**
   - Lam tron so tien cuoi cung den hang nghin dong gan nhat ($\text{Round}(\dots, -3)$) de tao so tien chuyen khoan thuan tien (vi du: 53.400d -> 53.000d hoac 54.000d theo cau hinh CLB).

---

## 3. Quy Tac Doi Soat Ngan Hang Webhook (FR21 & Idempotency)

1. **Cu phap chuyen khoan dinh danh bat buoc (`TransferSyntax`):**
   - Chuoi thanh toan VietQR bat buoc chua noi dung: `CLB {SessionIdShort} {UserIdShort}` (vi du: `CLB 8A2F E412`).
   - Giup he thong tu dong boc tach va doi soat 100% khong nham lan.
2. **Tinh Idempotency (Chong xu ly lap lai Webhook):**
   - Cong thanh toan co the gui lai Webhook nhieu lan cho cung 1 giao dich thanh cong.
   - Khi tiep nhan Webhook:
     - Kiem tra `reference_code` tu ngan hang:
       - Neu da ton tai trong `bank_transaction` va `session_fee.status == PAID`: Bo qua va tra ve HTTP `200 OK` ngay lap tuc.
       - Neu chua xu ly: Doi trang thai sang `PAID`, gan `paid_at = now()`, luu log `bank_transaction`, va ghi 1 dong thu `INCOME` vao `group_ledger`.
3. **Xac thuc Chu ky Webhook (Security):**
   - Bat buoc kiem tra ma bi mat `webhook_secret_key` trong HTTP Header truoc khi xu ly payload.

---

## 4. Tinh Toan Ven Giao Dich Tai Chinh (ACID & Unit of Work)

- Moi thao tac lam thay doi so du quy nhom (`GroupLedgerEntry`) hoac chot so buoi choi (`SessionFee`) bat buoc phai duoc boc trong mot **Database Transaction** (`UnitOfWorkBehavior`).
- Tuyet doi khong de xay ra tinh trang: Hoa don `SessionFee` da chuyen `PAID` nhung So quy `GroupLedgerEntry` chua ghi nhan dong tien vao. Neu mot trong hai thao tac that bai, toan bo giao dich phai duoc `Rollback`.
