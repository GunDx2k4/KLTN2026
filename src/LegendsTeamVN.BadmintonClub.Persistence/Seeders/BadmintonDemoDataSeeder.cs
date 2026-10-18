using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Persistence.Seeders;

public class BadmintonDemoDataSeeder(
    BadmintonDbContext dbContext,
    ILogger<BadmintonDemoDataSeeder> logger) : IDataSeeder
{
    public int Order => 4;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Ensuring Badminton demo clubs, members, schedules, and sessions are seeded...");

        // Lấy danh sách users đã được seed từ BadmintonUserSeeder
        var users = await dbContext.AppUsers.ToListAsync(cancellationToken);
        if (users.Count == 0)
        {
            logger.LogWarning("No AppUsers found. Skipping demo data seeding.");
            return;
        }

        var userDict = users.ToDictionary(u => u.EmailAddress ?? "", StringComparer.OrdinalIgnoreCase);

        var admin = userDict.GetValueOrDefault("admin@l7ungdz.id.vn") ?? users[0];
        var user1 = userDict.GetValueOrDefault("user1@l7ungdz.id.vn") ?? users[0];
        var user2 = userDict.GetValueOrDefault("user2@l7ungdz.id.vn") ?? (users.Count > 1 ? users[1] : users[0]);
        var user3 = userDict.GetValueOrDefault("user3@l7ungdz.id.vn") ?? (users.Count > 2 ? users[2] : users[0]);
        var user4 = userDict.GetValueOrDefault("user4@l7ungdz.id.vn") ?? users[0];
        var user5 = userDict.GetValueOrDefault("user5@l7ungdz.id.vn") ?? users[0];
        var user6 = userDict.GetValueOrDefault("user6@l7ungdz.id.vn") ?? users[0];
        var user7 = userDict.GetValueOrDefault("user7@l7ungdz.id.vn") ?? users[0];
        var user8 = userDict.GetValueOrDefault("user8@l7ungdz.id.vn") ?? users[0];

        var allPermissions = await dbContext.GroupPermissions.ToListAsync(cancellationToken);

        // Kiểm tra xem CLB chính đã tồn tại chưa
        var existingClub = await dbContext.ClubGroups.FirstOrDefaultAsync(c => c.Code == "CLB-LEGENDS7", cancellationToken);
        if (existingClub == null)
        {
            // =========================================================================
            // CLB 1: CLB Cầu Lông Legends Quận 7 (Host: user1)
            // =========================================================================
            var club1 = new ClubGroup(
                groupName: "CLB Cầu Lông Legends Quận 7",
                description: "Nhóm cầu lông phong trào nhiệt huyết, sinh hoạt đều đặn tối thứ 3 - 5 - 7 tại Quận 7. Đề cao tinh thần giao lưu học hỏi và rèn luyện thể lực.",
                avatarUrl: "https://images.unsplash.com/photo-1626224583764-f87db24ac4ea?w=300",
                fixedVenueName: "Sân Cầu Lông Kim Cương",
                fixedAddress: "123 Huỳnh Tấn Phát, Phường Tân Thuận Đông, Quận 7, TP.HCM",
                lat: 10.7412m,
                lng: 106.7231m,
                minSkillLevel: SkillLevel.TB,
                maxSkillLevel: SkillLevel.K,
                groupQualityScore: 4.90m,
                isRecruiting: true,
                isActive: true,
                code: "CLB-LEGENDS7"
            );
            await dbContext.ClubGroups.AddAsync(club1, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var (c1Host, c1Deputy, c1Treasurer, c1Member, c1Guest) = await CreateClubRolesAsync(club1, allPermissions, cancellationToken);

            // Members
            dbContext.GroupMembers.AddRange(
                new GroupMember(club1.Id, user1.Id, c1Host.Id, "MONTHLY", DateTime.UtcNow.AddMonths(6), ClubMemberStatus.ACTIVE, "Dũng Smash (Host)"),
                new GroupMember(club1.Id, user2.Id, c1Deputy.Id, "MONTHLY", DateTime.UtcNow.AddMonths(3), ClubMemberStatus.ACTIVE, "Mai Cầu Lông"),
                new GroupMember(club1.Id, user3.Id, c1Treasurer.Id, "MONTHLY", DateTime.UtcNow.AddMonths(3), ClubMemberStatus.ACTIVE, "Long Thủ Quỹ"),
                new GroupMember(club1.Id, user4.Id, c1Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(1), ClubMemberStatus.ACTIVE, "Tuấn Anh"),
                new GroupMember(club1.Id, user5.Id, c1Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(2), ClubMemberStatus.ACTIVE, "Triết Pro"),
                new GroupMember(club1.Id, user6.Id, c1Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Thảo Đặng"),
                new GroupMember(club1.Id, user7.Id, c1Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Bảo Hoàng")
            );

            // Schedules
            dbContext.GroupSchedules.AddRange(
                new GroupSchedule(club1.Id, 2, new TimeOnly(18, 0), new TimeOnly(21, 0), true),
                new GroupSchedule(club1.Id, 4, new TimeOnly(18, 0), new TimeOnly(21, 0), true),
                new GroupSchedule(club1.Id, 6, new TimeOnly(14, 0), new TimeOnly(18, 0), true)
            );

            // Bank Account & Fee Rule
            dbContext.GroupBankAccounts.Add(new GroupBankAccount(club1.Id, "MBBank", "0987654321", "NGUYEN VAN DUNG"));
            dbContext.GroupFeeRules.Add(new GroupFeeRule(club1.Id, FeeMode.DYNAMIC, monthlyFee: 350000, dailyMemberFee: 40000, guestFee: 50000, femaleDiscountDaily: 5000));

            // Join Requests (user8 pending, user7 approved)
            dbContext.JoinRequests.AddRange(
                new JoinRequest(club1.Id, user8.Id, "Em chào Host Dũng, em mới tập chơi 6 tháng, muốn xin vào CLB để giao lưu học hỏi ạ!", JoinRequestStatus.PENDING),
                new JoinRequest(club1.Id, user7.Id, "Xin gia nhập nhóm cầu lông Quận 7", JoinRequestStatus.APPROVED)
            );

            // Sessions
            // Session 1: Sắp diễn ra (OPEN, còn slot, user1 đã có vé CONFIRMED)
            var s1 = new BadmintonSession(
                club1.Id,
                "Kèo tối thứ 5 giao lưu sân Kim Cương",
                DateTime.UtcNow.AddDays(1).Date.AddHours(18),
                DateTime.UtcNow.AddDays(1).Date.AddHours(21),
                "Sân Cầu Lông Kim Cương, 123 Huỳnh Tấn Phát, Q.7",
                courtCount: 2,
                targetPlayerPerCourt: 6,
                maxPlayers: 12,
                courtRentalCost: 360000,
                status: SessionStatus.OPEN
            );
            await dbContext.BadmintonSessions.AddAsync(s1, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.SessionBookings.AddRange(
                new SessionBooking(s1.Id, user1.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s1.Id, user2.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s1.Id, user3.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s1.Id, user4.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s1.Id, user5.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s1.Id, user6.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s1.Id, user7.Id, null, BookingStatus.CONFIRMED, 0)
            );

            // Active Guest Recruitment for Session 1 (FR11)
            var r1 = new GuestRecruitment(s1.Id, slotsNeeded: 2, minSkillLevel: SkillLevel.TB, sharedCost: 50000, scanRadiusKm: 5.0m, status: RecruitmentStatus.ACTIVE);
            dbContext.GuestRecruitments.Add(r1);

            // Session 2: Đầy chỗ (OPEN, 6/6 confirmed, có hàng đợi chờ FR08-FR10)
            var s2 = new BadmintonSession(
                club1.Id,
                "Kèo tối thứ 7 đập cầu cọ xát",
                DateTime.UtcNow.AddDays(3).Date.AddHours(14),
                DateTime.UtcNow.AddDays(3).Date.AddHours(18),
                "Sân Cầu Lông Kim Cương, 123 Huỳnh Tấn Phát, Q.7",
                courtCount: 1,
                targetPlayerPerCourt: 6,
                maxPlayers: 6,
                courtRentalCost: 240000,
                status: SessionStatus.OPEN
            );
            await dbContext.BadmintonSessions.AddAsync(s2, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.SessionBookings.AddRange(
                new SessionBooking(s2.Id, user2.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s2.Id, user3.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s2.Id, user4.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s2.Id, user5.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s2.Id, user6.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s2.Id, user7.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s2.Id, user8.Id, null, BookingStatus.WAITING, 1)
            );

            // Session 3: Đang thi đấu (PLAYING)
            var s3 = new BadmintonSession(
                club1.Id,
                "Buổi sinh hoạt định kỳ đang thi đấu",
                DateTime.UtcNow.AddHours(-1),
                DateTime.UtcNow.AddHours(2),
                "Sân Cầu Lông Kim Cương, Q.7",
                courtCount: 2,
                maxPlayers: 12,
                status: SessionStatus.PLAYING
            );
            await dbContext.BadmintonSessions.AddAsync(s3, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var b1 = CreateCheckedInBooking(s3.Id, user1.Id, user1.Id, s3.StartTime);
            var b2 = CreateCheckedInBooking(s3.Id, user2.Id, user1.Id, s3.StartTime);
            s3.CompleteAttendance(user1.Id, s3.StartTime);
            dbContext.SessionBookings.AddRange(b1, b2, new SessionBooking(s3.Id, user3.Id, null, BookingStatus.CONFIRMED, 0));

            // Session 4: Hoàn thành tuần trước (COMPLETED)
            var s4 = new BadmintonSession(
                club1.Id,
                "Kèo giao lưu tuần trước",
                DateTime.UtcNow.AddDays(-7).Date.AddHours(18),
                DateTime.UtcNow.AddDays(-7).Date.AddHours(21),
                "Sân Cầu Lông Kim Cương, Q.7",
                courtCount: 2,
                maxPlayers: 12,
                courtRentalCost: 360000,
                status: SessionStatus.COMPLETED
            );
            await dbContext.BadmintonSessions.AddAsync(s4, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.SessionBookings.AddRange(
                CreateCheckedInBooking(s4.Id, user1.Id, user1.Id, s4.StartTime),
                CreateCheckedInBooking(s4.Id, user2.Id, user1.Id, s4.StartTime),
                CreateCheckedInBooking(s4.Id, user3.Id, user1.Id, s4.StartTime)
            );
            s4.CompleteAttendance(user1.Id, s4.StartTime);

            // Session Fees
            dbContext.SessionFees.AddRange(
                new SessionFee(s4.Id, user1.Id, 40000, PriceType.DAILY_MEMBER, "FEE-LEGENDS-001", "vietqr-payload-demo-1", PaymentMethod.BANK_TRANSFER, paymentStatus: PaymentStatus.PAID),
                new SessionFee(s4.Id, user2.Id, 35000, PriceType.DAILY_MEMBER, "FEE-LEGENDS-002", "vietqr-payload-demo-2", PaymentMethod.BANK_TRANSFER, discountApplied: 5000, paymentStatus: PaymentStatus.PAID),
                new SessionFee(s4.Id, user4.Id, 50000, PriceType.GUEST, "FEE-LEGENDS-003", "vietqr-payload-demo-3", PaymentMethod.CASH, paymentStatus: PaymentStatus.PAID)
            );

            // Group Ledger Transactions (Sổ quỹ CLB 1)
            dbContext.GroupLedgers.AddRange(
                new GroupLedger(club1.Id, user1.Id, TransactionType.INFLOW, LedgerCategory.MONTHLY_FUND, 2450000, "Thu tiền quỹ tháng 7 thành viên cố định"),
                new GroupLedger(club1.Id, user1.Id, TransactionType.OUTFLOW, LedgerCategory.COURT_RENT, 720000, "Thanh toán tiền sân Kim Cương 2 buổi"),
                new GroupLedger(club1.Id, user1.Id, TransactionType.OUTFLOW, LedgerCategory.SHUTTLE_EXPENSE, 500000, "Mua 2 ống cầu Victor Lark 5"),
                new GroupLedger(club1.Id, user1.Id, TransactionType.INFLOW, LedgerCategory.SESSION_FEE, 150000, "Thu tiền 3 khách vãng lai giao lưu", s4.Id)
            );

            // Reviews
            dbContext.GroupReviews.AddRange(
                new GroupReview(user2.Id, club1.Id, 5, "Sân bãi tốt, đồng đội thân thiện", "Nhóm sinh hoạt rất vui và đúng giờ!"),
                new GroupReview(user3.Id, club1.Id, 5, "Uy tín", "Host tổ chức chu đáo, nước uống và cầu thi đấu chất lượng cao."),
                new GroupReview(user4.Id, club1.Id, 4, "Tốt", "Kèo đánh vừa tầm, rất đáng tham gia!")
            );

            // =========================================================================
            // CLB 2: CLB Cầu Lông Chiến Binh Quận 10 (Host: admin, user1 là Thành viên)
            // =========================================================================
            var club2 = new ClubGroup(
                groupName: "CLB Cầu Lông Chiến Binh Quận 10",
                description: "Nhóm sinh hoạt phong trào vui vẻ, đề cao tinh thần giao lưu học hỏi tại trung tâm Quận 10.",
                avatarUrl: "https://images.unsplash.com/photo-1541534741688-6078c6bfb5c5?w=300",
                fixedVenueName: "Sân Cầu Lông Kỳ Hòa",
                fixedAddress: "238 Đường 3/2, Phường 12, Quận 10, TP.HCM",
                lat: 10.7769m,
                lng: 106.6958m,
                minSkillLevel: SkillLevel.TBY,
                maxSkillLevel: SkillLevel.TBK,
                groupQualityScore: 4.80m,
                isRecruiting: true,
                isActive: true,
                code: "CLB-CBQ10"
            );
            await dbContext.ClubGroups.AddAsync(club2, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var (c2Host, c2Deputy, c2Treasurer, c2Member, c2Guest) = await CreateClubRolesAsync(club2, allPermissions, cancellationToken);

            dbContext.GroupMembers.AddRange(
                new GroupMember(club2.Id, admin.Id, c2Host.Id, "MONTHLY", DateTime.UtcNow.AddMonths(12), ClubMemberStatus.ACTIVE, "Admin Host"),
                new GroupMember(club2.Id, user1.Id, c2Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(3), ClubMemberStatus.ACTIVE, "Dũng Hội Viên"),
                new GroupMember(club2.Id, user2.Id, c2Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(2), ClubMemberStatus.ACTIVE, "Mai Mai"),
                new GroupMember(club2.Id, user4.Id, c2Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Tuấn Anh Q10"),
                new GroupMember(club2.Id, user5.Id, c2Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(1), ClubMemberStatus.ACTIVE, "Triết Giao Lưu")
            );

            dbContext.GroupSchedules.AddRange(
                new GroupSchedule(club2.Id, 1, new TimeOnly(19, 0), new TimeOnly(22, 0), true),
                new GroupSchedule(club2.Id, 3, new TimeOnly(19, 0), new TimeOnly(22, 0), true),
                new GroupSchedule(club2.Id, 5, new TimeOnly(19, 0), new TimeOnly(22, 0), true)
            );

            dbContext.GroupBankAccounts.Add(new GroupBankAccount(club2.Id, "Vietcombank", "0123456789", "HE THONG ADMIN"));
            dbContext.GroupFeeRules.Add(new GroupFeeRule(club2.Id, FeeMode.DYNAMIC, monthlyFee: 400000, dailyMemberFee: 45000, guestFee: 50000));

            // Session 5 in CLB 2: Có tin tuyển khách vãng lai đang ACTIVE (FR11, FR12)
            var s5 = new BadmintonSession(
                club2.Id,
                "Kèo tối thứ 4 giao lưu sân Kỳ Hòa",
                DateTime.UtcNow.AddDays(1).Date.AddHours(19),
                DateTime.UtcNow.AddDays(1).Date.AddHours(22),
                "Sân Cầu Lông Kỳ Hòa, 238 Đường 3/2, Quận 10",
                courtCount: 2,
                targetPlayerPerCourt: 6,
                maxPlayers: 12,
                courtRentalCost: 360000,
                status: SessionStatus.OPEN
            );
            await dbContext.BadmintonSessions.AddAsync(s5, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.SessionBookings.AddRange(
                new SessionBooking(s5.Id, admin.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s5.Id, user2.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s5.Id, user4.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s5.Id, user5.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s5.Id, user6.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(s5.Id, user7.Id, null, BookingStatus.CONFIRMED, 0)
            );

            // Active guest recruitment in CLB 2 (FR12 radar search target!)
            var r2 = new GuestRecruitment(s5.Id, slotsNeeded: 4, minSkillLevel: SkillLevel.TB, sharedCost: 50000, scanRadiusKm: 10.0m, status: RecruitmentStatus.ACTIVE);
            dbContext.GuestRecruitments.Add(r2);

            // =========================================================================
            // CLB 3: CLB Cầu Lông Tia Chớp Tân Bình (Host: user2)
            // =========================================================================
            var club3 = new ClubGroup(
                groupName: "CLB Cầu Lông Tia Chớp Tân Bình",
                description: "CLB chuyên cọ xát đánh giải phong trào, tập luyện kỹ chiến thuật đánh đôi bài bản.",
                avatarUrl: "https://images.unsplash.com/photo-1574629810360-7efbbe195018?w=300",
                fixedVenueName: "Sân Cầu Lông Viettel",
                fixedAddress: "158 Hoàng Hoa Thám, Phường 12, Quận Tân Bình, TP.HCM",
                lat: 10.8015m,
                lng: 106.6521m,
                minSkillLevel: SkillLevel.TB,
                maxSkillLevel: SkillLevel.K,
                groupQualityScore: 4.75m,
                isRecruiting: false,
                isActive: true,
                code: "CLB-TIACHOP"
            );
            await dbContext.ClubGroups.AddAsync(club3, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var (c3Host, _, _, c3Member, _) = await CreateClubRolesAsync(club3, allPermissions, cancellationToken);
            dbContext.GroupMembers.AddRange(
                new GroupMember(club3.Id, user2.Id, c3Host.Id, "MONTHLY", DateTime.UtcNow.AddMonths(6), ClubMemberStatus.ACTIVE, "Mai Host"),
                new GroupMember(club3.Id, user3.Id, c3Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(3), ClubMemberStatus.ACTIVE, "Long TB"),
                new GroupMember(club3.Id, user5.Id, c3Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(3), ClubMemberStatus.ACTIVE, "Triết Pro"),
                new GroupMember(club3.Id, user7.Id, c3Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Bảo TB")
            );
            dbContext.GroupSchedules.AddRange(
                new GroupSchedule(club3.Id, 6, new TimeOnly(8, 0), new TimeOnly(11, 0), true),
                new GroupSchedule(club3.Id, 0, new TimeOnly(8, 0), new TimeOnly(11, 0), true)
            );

            // =========================================================================
            // CLB 4: CLB Cầu Lông Sinh Viên Bách Khoa (user1 có đơn PENDING - FR07 Action Bar!)
            // =========================================================================
            var club4 = new ClubGroup(
                groupName: "CLB Cầu Lông Sinh Viên Bách Khoa",
                description: "Sân chơi thể thao lành mạnh cho sinh viên và cựu sinh viên Bách Khoa, mở rộng cho người mới bắt đầu.",
                avatarUrl: "https://images.unsplash.com/photo-1517649763962-0c623266ddc0?w=300",
                fixedVenueName: "Nhà Thi Đấu Bách Khoa",
                fixedAddress: "268 Lý Thường Kiệt, Phường 14, Quận 10, TP.HCM",
                lat: 10.7725m,
                lng: 106.6598m,
                minSkillLevel: SkillLevel.NB,
                maxSkillLevel: SkillLevel.TB,
                groupQualityScore: 4.60m,
                isRecruiting: true,
                isActive: true,
                code: "CLB-SVBK"
            );
            await dbContext.ClubGroups.AddAsync(club4, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var (c4Host, _, _, c4Member, _) = await CreateClubRolesAsync(club4, allPermissions, cancellationToken);
            dbContext.GroupMembers.AddRange(
                new GroupMember(club4.Id, user3.Id, c4Host.Id, "MONTHLY", DateTime.UtcNow.AddMonths(6), ClubMemberStatus.ACTIVE, "Long BK"),
                new GroupMember(club4.Id, user4.Id, c4Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(2), ClubMemberStatus.ACTIVE, "Tuấn Anh BK"),
                new GroupMember(club4.Id, user6.Id, c4Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Thảo BK"),
                new GroupMember(club4.Id, user8.Id, c4Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Linh BK")
            );
            dbContext.GroupSchedules.AddRange(
                new GroupSchedule(club4.Id, 2, new TimeOnly(17, 0), new TimeOnly(19, 0), true),
                new GroupSchedule(club4.Id, 4, new TimeOnly(17, 0), new TimeOnly(19, 0), true)
            );

            // user1 gửi đơn gia nhập CLB 4 đang PENDING (giúp test nút vàng 'Đơn đang chờ duyệt' trên Mobile!)
            dbContext.JoinRequests.Add(
                new JoinRequest(club4.Id, user1.Id, "Chào CLB, mình muốn tham gia sinh hoạt cùng các bạn vào các buổi chiều tối!", JoinRequestStatus.PENDING)
            );

            // =========================================================================
            // CLB 5: CLB Cầu Lông Đỉnh Cao Bình Thạnh (Host: user5)
            // =========================================================================
            var club5 = new ClubGroup(
                groupName: "CLB Cầu Lông Đỉnh Cao Bình Thạnh",
                description: "Hội tụ các tay vợt bán chuyên và cựu VĐV năng khiếu, giao lưu trình độ khá giỏi cọ xát đỉnh cao.",
                avatarUrl: "https://images.unsplash.com/photo-1508214751196-bcfd4ca60f91?w=300",
                fixedVenueName: "Sân Cầu Lông Bình Thạnh",
                fixedAddress: "80 Chu Văn An, Phường 26, Quận Bình Thạnh, TP.HCM",
                lat: 10.8142m,
                lng: 106.7089m,
                minSkillLevel: SkillLevel.TBK,
                maxSkillLevel: SkillLevel.K,
                groupQualityScore: 4.95m,
                isRecruiting: true,
                isActive: true,
                code: "CLB-DCBT"
            );
            await dbContext.ClubGroups.AddAsync(club5, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var (c5Host, _, _, c5Member, _) = await CreateClubRolesAsync(club5, allPermissions, cancellationToken);
            dbContext.GroupMembers.AddRange(
                new GroupMember(club5.Id, user5.Id, c5Host.Id, "MONTHLY", DateTime.UtcNow.AddMonths(12), ClubMemberStatus.ACTIVE, "Triết Pro"),
                new GroupMember(club5.Id, user3.Id, c5Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(3), ClubMemberStatus.ACTIVE, "Long Khá"),
                new GroupMember(club5.Id, user7.Id, c5Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Bảo Khá")
            );
            dbContext.GroupSchedules.AddRange(
                new GroupSchedule(club5.Id, 1, new TimeOnly(18, 0), new TimeOnly(20, 30), true),
                new GroupSchedule(club5.Id, 3, new TimeOnly(18, 0), new TimeOnly(20, 30), true),
                new GroupSchedule(club5.Id, 5, new TimeOnly(18, 0), new TimeOnly(20, 30), true)
            );

            // =========================================================================
            // CLB 6: CLB Cầu Lông Phủi Phú Nhuận (Host: admin)
            // =========================================================================
            var club6 = new ClubGroup(
                groupName: "CLB Cầu Lông Phủi Phú Nhuận",
                description: "Giao lưu vui khỏe cuối tuần, không phân biệt trình độ, có người hướng dẫn cho người mới.",
                avatarUrl: "https://images.unsplash.com/photo-1461896836934-ffe607ba8211?w=300",
                fixedVenueName: "Sân Cầu Lông Ga Trực Thăng",
                fixedAddress: "Hoàng Hoa Thám, Phường 7, Quận Phú Nhuận, TP.HCM",
                lat: 10.8021m,
                lng: 106.6874m,
                minSkillLevel: SkillLevel.Y,
                maxSkillLevel: SkillLevel.TB,
                groupQualityScore: 4.40m,
                isRecruiting: true,
                isActive: true,
                code: "CLB-PHUIPN"
            );
            await dbContext.ClubGroups.AddAsync(club6, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var (c6Host, _, _, c6Member, _) = await CreateClubRolesAsync(club6, allPermissions, cancellationToken);
            dbContext.GroupMembers.AddRange(
                new GroupMember(club6.Id, admin.Id, c6Host.Id, "MONTHLY", DateTime.UtcNow.AddMonths(12), ClubMemberStatus.ACTIVE, "Admin"),
                new GroupMember(club6.Id, user6.Id, c6Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Thảo Y"),
                new GroupMember(club6.Id, user8.Id, c6Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Linh NB")
            );
            dbContext.GroupSchedules.AddRange(
                new GroupSchedule(club6.Id, 6, new TimeOnly(15, 0), new TimeOnly(18, 0), true),
                new GroupSchedule(club6.Id, 0, new TimeOnly(15, 0), new TimeOnly(18, 0), true)
            );

            // =========================================================================
            // Thông Báo Mẫu (Notifications)
            // =========================================================================
            dbContext.Notifications.AddRange(
                new Notification(user1.Id, "Đơn gia nhập nhóm", "Bạn đã được duyệt làm thành viên của CLB Cầu Lông Chiến Binh Quận 10", NotificationType.QUEUE),
                new Notification(user1.Id, "Nhắc nhở buổi chơi", "Buổi chơi tối thứ 5 tại sân Kim Cương sẽ bắt đầu lúc 18:00. Hãy đến sân đúng giờ nhé!", NotificationType.SESSION),
                new Notification(user1.Id, "Yêu cầu gia nhập mới", "Thành viên Bùi Phương Linh vừa gửi đơn xin gia nhập CLB Cầu Lông Legends Quận 7", NotificationType.QUEUE),
                new Notification(admin.Id, "Thông báo hệ thống", "Hệ thống quản lý nhóm cầu lông LegendsTeamVN đã cập nhật phiên bản 2.1", NotificationType.SESSION)
            );

            // Member Review
            dbContext.MemberReviews.Add(
                new MemberReview(user2.Id, user1.Id, 5, "Nhiệt tình", "Đánh đôi rất ăn ý, hỗ trợ đồng đội rất tốt.")
            );

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Demo clubs, roles, members, sessions, bookings and recruitments seeded successfully.");
        }
        else
        {
            logger.LogInformation("Demo data already exists. Skipping.");
        }
    }

    private static SessionBooking CreateCheckedInBooking(Guid sessionId, Guid userId, Guid actorId, DateTime markedAt)
    {
        var booking = new SessionBooking(sessionId, userId);
        booking.MarkPresent(actorId, markedAt);
        return booking;
    }

    private async Task<(GroupRole hostRole, GroupRole deputyRole, GroupRole treasurerRole, GroupRole memberRole, GroupRole guestRole)> CreateClubRolesAsync(
        ClubGroup club,
        List<GroupPermission> allPermissions,
        CancellationToken cancellationToken)
    {
        var hostRole = new GroupRole(club.Id, "Host", "Chủ phòng sở hữu toàn quyền quản trị", isDefault: false);
        var deputyRole = new GroupRole(club.Id, "Phó nhóm", "Hỗ trợ Host điều hành buổi chơi và thành viên", isDefault: false);
        var treasurerRole = new GroupRole(club.Id, "Thủ quỹ", "Quản lý thu chi và quỹ nhóm", isDefault: false);
        var memberRole = new GroupRole(club.Id, "Thành viên", "Thành viên chính thức của câu lạc bộ", isDefault: true);
        var guestRole = new GroupRole(club.Id, "Khách vãng lai", "Người chơi tham gia giao lưu theo buổi", isDefault: false);

        dbContext.GroupRoles.AddRange(hostRole, deputyRole, treasurerRole, memberRole, guestRole);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Assign permissions
        await GroupPermissionSeeder.AssignRolePermissionsAsync(dbContext, hostRole.Id, "Host", allPermissions, cancellationToken);
        await GroupPermissionSeeder.AssignRolePermissionsAsync(dbContext, treasurerRole.Id, "Thủ quỹ", allPermissions, cancellationToken);
        await GroupPermissionSeeder.AssignRolePermissionsAsync(dbContext, memberRole.Id, "Thành viên", allPermissions, cancellationToken);
        await GroupPermissionSeeder.AssignRolePermissionsAsync(dbContext, guestRole.Id, "Khách vãng lai", allPermissions, cancellationToken);

        // Deputy gets all permissions except Member.RoleManage
        var deputyCodes = GroupPermissions.All
            .Where(p => p.Code != GroupPermissions.Member.RoleManage)
            .Select(p => p.Code)
            .ToList();
        var deputyPermIds = allPermissions
            .Where(p => deputyCodes.Contains(p.PermissionCode, StringComparer.OrdinalIgnoreCase))
            .Select(p => p.Id)
            .ToList();
        foreach (var pid in deputyPermIds)
        {
            dbContext.RolePermissions.Add(new RolePermission(deputyRole.Id, pid));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return (hostRole, deputyRole, treasurerRole, memberRole, guestRole);
    }
}
