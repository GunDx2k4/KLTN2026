---
trigger: model_decision
description: "Single Source of Truth (SSOT) alignment with docs/ specifications (23 FRs, 20 UCs, 22 3NF tables, RESTful routes, state machines). Use when implementing new features, checking entity schemas, or verifying use case workflows."
---

# Quy Tac Tuan Thu Dac Ta Phan Tich & Thiet Ke He Thong (System Spec Alignment)

> [!IMPORTANT]
> Toan bo AI Agent va ky su phan mem khi lam viec tren du an LegendsTeamVN.BadmintonClub phai coi thu muc `docs/` la **Nguon Chan Ly Duy Nhat (Single Source of Truth - SSOT)**. Tuyet doi khong tu suy dien hoac bia dat logic nghiep vu, ten bang, ten cot, cong thuc tinh toan hoac luong chuyen doi trang thai khac voi tai lieu thiet ke.

---

## 1. Ban Do Nguon Chan Ly Ky Thuat (SSOT Directory)

Moi thay doi ma nguon phai duoc doi soat voi cac tai lieu sau truoc khi viet code:

| Tai Lieu Thiet Ke | Duong Dan | Noi Dung Trong Yeu Can Doi Soat |
| :--- | :--- | :--- |
| **Yeu Cau Chuc Nang (FR)** | [`docs/02_KHAO_SAT_VA_YEU_CAU.md`](docs/02_KHAO_SAT_VA_YEU_CAU.md) | 23 Yeu cau chuc nang chi tiet tu **FR01** den **FR23**. |
| **Phan Tich Ca Su Dung (UC)** | [`docs/03_PHAN_TICH_HE_THONG_UC.md`](docs/03_PHAN_TICH_HE_THONG_UC.md) | 20 Use Cases (**UC01** - **UC20**) gom Tac nhan, Tien dieu kien, Luong chinh va Hau dieu kien. |
| **Thiet Ke CSDL 3NF** | [`docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md`](docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md) | Cau truc 22 bang CSDL chuan hoa 3NF, kieu du lieu PostgreSQL, khoa ngoai, rang buoc. |
| **Tien Do & Danh Muc API** | [`docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md`](docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md) | Danh muc endpoint RESTful, routing path chuan, va trang thai hoan thien cua tung FR. |

---

## 2. Bang Ma Tran Nghiep Vu Chuan (5 Phan He - 23 FR - 20 UC - 22 Bang CSDL)

Khi lap trinh bat ky tinh nang nao, Agent bat buoc phai xac dinh chinh xac bo ba **(Phan he, FR/UC, Bang CSDL)**:

### 2.1. Phan He 1: Quan Tri Tai Khoan & Da Nhom (F1)
- **FR01 / UC01**: Dang ky tai khoan dinh danh -> Bang: `app_user` (BCrypt password hash).
- **FR02 / UC02**: Dang nhap, JWT phien lam viec & Dang xuat -> Bang: `app_user`, `user_device` (Device Token cho FCM).
- **FR03, FR04 / UC04, UC05**: Quan tri da nhom & Chuyen doi ngu canh nhom -> Bang: `club_group`, `group_role`, `group_permission`, `role_permission`, `group_member`.
  - *Bat bien*: Mot nguoi dung tham gia N CLB voi N vai tro doc lap; cap JWT tam thoi chua `tenantId` va `permissions` khi doi nhom.
- **FR05 / UC03**: Ho so ca nhan, cap trinh do (NB, Y, TBY, TB, TBK, K) & Bat/tat nhan giao luu -> Bang: `app_user`.

### 2.2. Phan He 2: Kham Pha, Giu Cho & Ket Noi (F2)
- **FR06, FR07 / UC06**: Tim kiem CLB quanh toa do GPS ban kinh km (cong thuc Haversine), xem thong tin CLB & lich tuan -> Bang: `club_group`, `group_schedule`.
- **FR08, FR09, FR10 / UC08, UC09**: Bieu quyet giu cho buoi sinh hoat co kiem soat tran suc chua san & Hang doi thong minh -> Bang: `badminton_session`, `session_booking`.
  - *Bat bien*: Khi dat tran `MaxPlayers`, nguoi tiep theo nhan trang thai `WAITING` voi `queue_order` tang dan; khi co nguoi `ABSENT`, tu dong don `queue_order = 1` len `CONFIRMED`. Bat buoc khoa phan tan **Redis Distributed Lock**.
- **FR11, FR12 / UC10, UC11**: Host phat radar tim khach giao luu lan can khi thieu quan & Khach dang ky slot -> Bang: `guest_recruitment`, `session_booking`.

### 2.3. Phan He 3: Van Hanh Buoi Choi & San Dau (F3)
- **FR13 / UC08**: Khoi tao buoi sinh hoat (Mo keo choi) -> Bang: `badminton_session`.
- **FR14 / UC12**: Diem danh nhanh tai san -> Bang: `session_booking` (`CheckIn()` -> `CHECKED_IN`, gan `checkin_at = UtcNow`).
- **FR15 / UC13**: Giai thuat tu dong de xuat 4 nguoi vao san xoay tua -> Bang: `session_match`, `match_player`.
  - *Bat bien giai thuat*: Uu tien thoi gian nghi lau nhat (chong ngoi ngoai lien tiep 2 tran) + ghep doi co chenh lech tong diem trinh do nho nhat.
- **FR16 / UC14**: Host can thiep hoan doi nguoi choi thu cong -> Gan `is_manual_swapped = true`.
- **FR17 / UC15**: Nguoi choi theo doi luot thi dau realtime qua SignalR (`SessionHub`).

### 2.4. Phan He 4: Hach Toan Vat Tu & So Quy Mo (F4)
- **FR18, FR19 / UC16**: Ghi nhan so qua cau boc moi (`shuttlecock_used`), tien thue san va quyet toan chi phi da he so -> Bang: `group_fee_rule`, `badminton_session`, `session_fee`.
  - *Cong thuc bat buoc*: Tong = Tien san + (So cau x Don gia cau) + Phu phi.
  - *Chiet khau & Lam tron*: Tru uu dai Nu (`female_discount_daily`), mien phi hoi vien thang (`MONTHLY`), lam tron len 1.000d theo `RoundRule`.
- **FR20 / UC17**: Sinh ma VietQR NAPAS 247 nhung san so tien va cu phap `CLB {SessionIdShort} {UserIdShort}`.
- **FR21 / UC18**: Doi soat Webhook SePay tu dong gach no (`session_fee.status = PAID`) -> Bang: `bank_transaction` (`MATCHED`), tu dong ghi dong thu `INCOME` vao `group_ledger`.
- **FR22 / UC19**: So quy mo minh bach (Thu, Chi, So du tuc thi) -> Bang: `group_ledger`.

### 2.5. Phan He 5: Danh Gia Tin Nhiem & Tuong Tac (F5)
- **FR23 / UC20**: Danh gia tin nhiem hai chieu (Diem thanh vien `reputation_score` & Diem chat luong CLB `group_quality_score`) -> Bang: `member_review`, `group_review`.

---

## 3. Quy Tac Bat Bien Ve Du Lieu & Trang Thai (State Machines)

### 3.1. May Trang Thai Cua Cac Thuc The Chinh:
1. **`BadmintonSession` (Buoi choi):**
   `OPEN` -> `LOCKED` -> `PLAYING` -> `CALCULATED` -> `COMPLETED`
   *(Chi duoc quyet toan khi o trang thai `PLAYING` hoac `FINISHED`).*
2. **`SessionBooking` (Giu cho):**
   `WAITING` <-> `CONFIRMED` -> `CHECKED_IN` hoac `CONFIRMED` -> `ABSENT`
3. **`SessionFee` (Khoan thu):**
   `UNPAID` -> `PAID` hoac `PARTIAL_PAID` -> `REFUNDED`
4. **`JoinRequest` (Don xin gia nhap):**
   `PENDING` -> `APPROVED` hoac `REJECTED`
5. **`GuestRecruitment` (Tuyen khach):**
   `ACTIVE` -> `FILLED` hoac `CANCELLED`

### 3.2. Cam Ky Ve Schema Co So Du Lieu:
- **CAM** tu y them bang moi hoac sua doi cau truc cot khi chua doi soat voi [`docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md`](docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md).
- **CAM** dung kieu du lieu `float` hay `double` cho bat ky cot tien te nao; toan bo phai dung kieu `decimal` trong C# va `numeric(12,2)` / `numeric(15,2)` trong PostgreSQL.
- **CAM** hard-code id vai tro hay chuoi phan quyen; su dung enum va hang so chuan hoa tu `LegendsTeamVN.BadmintonClub.Domain`.

---

## 4. Checklist Doi Soat Bat Buoc Truoc & Sau Khi Viet Code

- [ ] Da tra cuu Use Case muc tieu trong `docs/03_PHAN_TICH_HE_THONG_UC.md`.
- [ ] Da tra cuu bang CSDL va thuoc tinh trong `docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md`.
- [ ] Logic chuyen doi trang thai da duoc viet duoi dang **phuong thuc ben trong Domain Entity** (Rich Model).
- [ ] Duong dan route va HTTP Method chuan RESTful theo `docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md`.
- [ ] Chay kiem thu kien truc dam bao **37/37 NetArchTest tests Passed (100%)**.
