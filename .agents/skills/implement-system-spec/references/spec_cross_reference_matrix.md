# Ma Trận Đối Soát Toàn Diện: 23 FR - 20 UC - 22 Bảng CSDL 3NF (SSOT)

Nguồn chân lý duy nhất (SSOT): Thư mục [`docs/`](docs/).

## 1. Phân Hệ 1: Quản Trị Tài Khoản & Đa Nhóm (F1)
| Mã FR | Use Case | Bảng CSDL 3NF | C# Entity | Quyền Hạn / Bảo Mật |
| :---: | :---: | :--- | :--- | :--- |
| **FR01** | UC01 | `app_user` | `AppUser` | Public |
| **FR02** | UC02 | `app_user`, `user_device` | `AppUser`, `UserDevice` | Public / JWT Claims |
| **FR03** | UC04 | `club_group`, `group_role`, `group_permission`, `role_permission`, `group_member` | `ClubGroup`, `GroupRole`, `GroupPermission`, `RolePermission`, `GroupMember` | `GroupPermissions.Member.*` |
| **FR04** | UC05 | `group_member`, `group_role`, `role_permission` | `GroupMember`, `GroupRole` | Authenticated Member |
| **FR05** | UC03 | `app_user` | `AppUser` | Authenticated Self |

## 2. Phân Hệ 2: Khám Phá, Giữ Chỗ & Kết Nối Suất Chơi (F2)
| Mã FR | Use Case | Bảng CSDL 3NF | C# Entity | Quyền Hạn / Bảo Mật |
| :---: | :---: | :--- | :--- | :--- |
| **FR06** | UC06 | `club_group` | `ClubGroup` | Public (Haversine GPS) |
| **FR07** | UC06 | `club_group`, `group_schedule` | `ClubGroup`, `GroupSchedule` | Public |
| **FR08** | UC08 | `badminton_session`, `session_booking` | `BadmintonSession`, `SessionBooking` | `GroupPermissions.Operations.BookingManage` / Member |
| **FR09** | UC08 | `badminton_session`, `session_booking` | `BadmintonSession`, `SessionBooking` | Redis Distributed Lock |
| **FR10** | UC09 | `badminton_session`, `session_booking` | `BadmintonSession`, `SessionBooking` | Member (Self) / Host |
| **FR11** | UC10 | `guest_recruitment`, `badminton_session` | `GuestRecruitment`, `BadmintonSession` | `GroupPermissions.Operations.RecruitManage` |
| **FR12** | UC11 | `guest_recruitment`, `session_booking` | `GuestRecruitment`, `SessionBooking` | Authenticated Guest |

## 3. Phân Hệ 3: Vận Hành Buổi Chơi & Sân Đấu (F3)
| Mã FR | Use Case | Bảng CSDL 3NF | C# Entity | Quyền Hạn / Bảo Mật |
| :---: | :---: | :--- | :--- | :--- |
| **FR13** | UC08 | `badminton_session`, `club_group` | `BadmintonSession`, `ClubGroup` | `GroupPermissions.Operations.SessionCreate` |
| **FR14** | UC12 | `session_booking`, `badminton_session` | `SessionBooking`, `BadmintonSession` | `GroupPermissions.Operations.MatchManage` |
| **FR15** | UC13 | `session_match`, `match_player`, `app_user` | `SessionMatch`, `MatchPlayer`, `AppUser` | `GroupPermissions.Operations.MatchManage` |
| **FR16** | UC14 | `match_player`, `session_match` | `MatchPlayer`, `SessionMatch` | `GroupPermissions.Operations.MatchManage` |
| **FR17** | UC15 | `session_match`, `match_player` | `SessionMatch`, `MatchPlayer` | SignalR Hub `/hubs/session` |

## 4. Phân Hệ 4: Hạch Toán Vật Tư & Sổ Quỹ Mở (F4)
| Mã FR | Use Case | Bảng CSDL 3NF | C# Entity | Quyền Hạn / Bảo Mật |
| :---: | :---: | :--- | :--- | :--- |
| **FR18** | UC16 | `badminton_session`, `group_fee_rule` | `BadmintonSession`, `GroupFeeRule` | `GroupPermissions.Finance.FeeManage` |
| **FR19** | UC17 | `session_fee`, `badminton_session`, `group_fee_rule` | `SessionFee`, `BadmintonSession`, `GroupFeeRule` | `GroupPermissions.Finance.FeeManage` |
| **FR20** | UC18 | `session_fee`, `group_bank_account` | `SessionFee`, `GroupBankAccount` | Authenticated Member |
| **FR21** | UC18 | `bank_transaction`, `session_fee`, `group_ledger` | `BankTransaction`, `SessionFee`, `GroupLedger` | SePay Secret Key (Idempotency) |
| **FR22** | UC19 | `group_ledger`, `club_group` | `GroupLedger`, `ClubGroup` | `GroupPermissions.Finance.LedgerView/Manage` |

## 5. Phân Hệ 5: Đánh Giá Tín Nhiệm & Tương Tác (F5)
| Mã FR | Use Case | Bảng CSDL 3NF | C# Entity | Quyền Hạn / Bảo Mật |
| :---: | :---: | :--- | :--- | :--- |
| **FR23** | UC20 | `member_review`, `group_review` | `MemberReview`, `GroupReview` | Authenticated Member |
