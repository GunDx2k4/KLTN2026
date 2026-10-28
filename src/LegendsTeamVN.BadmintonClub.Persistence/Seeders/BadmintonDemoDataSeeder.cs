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

        var existingClub = await dbContext.ClubGroups.FirstOrDefaultAsync(c => c.Code == "CLB-CAUGIAY", cancellationToken);
        if (existingClub == null)
        {
            // =========================================================================
            // 1. CLB Cầu Lông Phong Trào Cầu Giấy (Host: user1)
            // Sinh hoạt: Thứ 3 & Thứ 5 (18:30 - 21:00)
            // =========================================================================
            var club1 = new ClubGroup(
                groupName: "CLB Cầu Lông Phong Trào Cầu Giấy",
                description: "Nhóm cầu lông phong trào sinh hoạt đều đặn tối thứ 3 - 5 tại Cầu Giấy. Tinh thần thể thao vui vẻ, cọ xát, nâng cao thể lực sau giờ làm việc.",
                avatarUrl: "https://images.unsplash.com/photo-1626224583764-f87db24ac4ea?w=300",
                fixedVenueName: "Sân Cầu Lông Nhà Thi Đấu Cầu Giấy",
                fixedAddress: "35 Trần Quý Kiên, Dịch Vọng, Quận Cầu Giấy, Hà Nội",
                lat: 21.0368m,
                lng: 105.7925m,
                minSkillLevel: SkillLevel.TB,
                maxSkillLevel: SkillLevel.K,
                groupQualityScore: 4.90m,
                isRecruiting: true,
                isActive: true,
                code: "CLB-CAUGIAY"
            );
            await dbContext.ClubGroups.AddAsync(club1, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var (c1Host, c1Deputy, c1Treasurer, c1Member, c1Guest) = await CreateClubRolesAsync(club1, allPermissions, cancellationToken);

            dbContext.GroupMembers.AddRange(
                new GroupMember(club1.Id, user1.Id, c1Host.Id, "MONTHLY", DateTime.UtcNow.AddMonths(6), ClubMemberStatus.ACTIVE, "Trần Việt Dũng (Host)"),
                new GroupMember(club1.Id, user2.Id, c1Deputy.Id, "MONTHLY", DateTime.UtcNow.AddMonths(3), ClubMemberStatus.ACTIVE, "Phương Mai"),
                new GroupMember(club1.Id, user3.Id, c1Treasurer.Id, "MONTHLY", DateTime.UtcNow.AddMonths(3), ClubMemberStatus.ACTIVE, "Lê Thăng Long"),
                new GroupMember(club1.Id, user4.Id, c1Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(1), ClubMemberStatus.ACTIVE, "Phạm Tuấn Anh"),
                new GroupMember(club1.Id, user5.Id, c1Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(2), ClubMemberStatus.ACTIVE, "Vũ Hoàng Minh"),
                new GroupMember(club1.Id, user6.Id, c1Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Đặng Phương Thảo"),
                new GroupMember(club1.Id, user7.Id, c1Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Hoàng Quốc Bảo")
            );

            dbContext.GroupSchedules.AddRange(
                new GroupSchedule(club1.Id, 2, new TimeOnly(18, 30), new TimeOnly(21, 0), true),
                new GroupSchedule(club1.Id, 4, new TimeOnly(18, 30), new TimeOnly(21, 0), true)
            );

            dbContext.GroupBankAccounts.Add(new GroupBankAccount(club1.Id, "MBBank", "0901234567", "TRAN VIET DUNG"));
            dbContext.GroupFeeRules.Add(new GroupFeeRule(club1.Id, FeeMode.DYNAMIC, monthlyFee: 350000, dailyMemberFee: 40000, guestFee: 50000, femaleDiscountDaily: 5000, femaleDiscountGuest: 10000));

            dbContext.JoinRequests.AddRange(
                new JoinRequest(club1.Id, user8.Id, "Em chào anh Dũng, em mới tập đánh được nửa năm ở khu Hoàng Mai, muốn xin vào CLB mình giao lưu học hỏi ạ!", JoinRequestStatus.PENDING),
                new JoinRequest(club1.Id, user7.Id, "Xin gia nhập nhóm cầu lông Cầu Giấy cọ xát", JoinRequestStatus.APPROVED)
            );

            // =========================================================================
            // 2. CLB Cầu Lông Đống Đa Warriors (Host: admin)
            // Sinh hoạt: Thứ 2 & Thứ 4 (19:30 - 22:00)
            // =========================================================================
            var club2 = new ClubGroup(
                groupName: "CLB Cầu Lông Đống Đa Warriors",
                description: "Nhóm tập trung các bạn trẻ đam mê cầu lông khu vực Đống Đa, Ba Đình. Đánh vui là chính, nâng cao sức khỏe sau giờ hành chính.",
                avatarUrl: "https://images.unsplash.com/photo-1541534741688-6078c6bfb5c5?w=300",
                fixedVenueName: "Nhà Thi Đấu Trịnh Hoài Đức",
                fixedAddress: "12 Trịnh Hoài Đức, Cát Linh, Quận Đống Đa, Hà Nội",
                lat: 21.0312m,
                lng: 105.8335m,
                minSkillLevel: SkillLevel.TBY,
                maxSkillLevel: SkillLevel.TBK,
                groupQualityScore: 4.80m,
                isRecruiting: true,
                isActive: true,
                code: "CLB-DONGDA"
            );
            await dbContext.ClubGroups.AddAsync(club2, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var (c2Host, c2Deputy, c2Treasurer, c2Member, c2Guest) = await CreateClubRolesAsync(club2, allPermissions, cancellationToken);

            dbContext.GroupMembers.AddRange(
                new GroupMember(club2.Id, admin.Id, c2Host.Id, "MONTHLY", DateTime.UtcNow.AddMonths(12), ClubMemberStatus.ACTIVE, "Tuấn Dũng (Host)"),
                new GroupMember(club2.Id, user1.Id, c2Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(3), ClubMemberStatus.ACTIVE, "Việt Dũng"),
                new GroupMember(club2.Id, user2.Id, c2Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(2), ClubMemberStatus.ACTIVE, "Phương Mai"),
                new GroupMember(club2.Id, user4.Id, c2Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Tuấn Anh"),
                new GroupMember(club2.Id, user5.Id, c2Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(1), ClubMemberStatus.ACTIVE, "Hoàng Minh")
            );

            dbContext.GroupSchedules.AddRange(
                new GroupSchedule(club2.Id, 1, new TimeOnly(19, 30), new TimeOnly(22, 0), true),
                new GroupSchedule(club2.Id, 3, new TimeOnly(19, 30), new TimeOnly(22, 0), true)
            );

            dbContext.GroupBankAccounts.Add(new GroupBankAccount(club2.Id, "Vietcombank", "0123456789", "NGUYEN TUAN DUNG"));
            dbContext.GroupFeeRules.Add(new GroupFeeRule(club2.Id, FeeMode.DYNAMIC, monthlyFee: 400000, dailyMemberFee: 45000, guestFee: 50000, femaleDiscountDaily: 5000, femaleDiscountGuest: 10000));

            // =========================================================================
            // 3. CLB Cầu Lông Hồ Tây Gió Lộng (Host: user2)
            // Sinh hoạt: Sáng Thứ 7 & Sáng Chủ Nhật (07:30 - 10:30)
            // =========================================================================
            var club3 = new ClubGroup(
                groupName: "CLB Cầu Lông Hồ Tây Gió Lộng",
                description: "Nhóm cầu lông sinh hoạt sáng cuối tuần gần Hồ Tây. Không khí trong lành, tập luyện kỹ chiến thuật đánh đôi nhịp nhàng.",
                avatarUrl: "https://images.unsplash.com/photo-1574629810360-7efbbe195018?w=300",
                fixedVenueName: "Sân Cầu Lông Tây Hồ",
                fixedAddress: "101 Xuân Diệu, Phường Quảng An, Quận Tây Hồ, Hà Nội",
                lat: 21.0615m,
                lng: 105.8285m,
                minSkillLevel: SkillLevel.TB,
                maxSkillLevel: SkillLevel.K,
                groupQualityScore: 4.75m,
                isRecruiting: false,
                isActive: true,
                code: "CLB-HOTAY"
            );
            await dbContext.ClubGroups.AddAsync(club3, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var (c3Host, _, _, c3Member, _) = await CreateClubRolesAsync(club3, allPermissions, cancellationToken);
            dbContext.GroupMembers.AddRange(
                new GroupMember(club3.Id, user2.Id, c3Host.Id, "MONTHLY", DateTime.UtcNow.AddMonths(6), ClubMemberStatus.ACTIVE, "Phương Mai (Host)"),
                new GroupMember(club3.Id, user3.Id, c3Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(3), ClubMemberStatus.ACTIVE, "Thăng Long"),
                new GroupMember(club3.Id, user5.Id, c3Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(3), ClubMemberStatus.ACTIVE, "Hoàng Minh"),
                new GroupMember(club3.Id, user7.Id, c3Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Quốc Bảo")
            );
            dbContext.GroupSchedules.AddRange(
                new GroupSchedule(club3.Id, 6, new TimeOnly(7, 30), new TimeOnly(10, 30), true),
                new GroupSchedule(club3.Id, 0, new TimeOnly(7, 30), new TimeOnly(10, 30), true)
            );

            // =========================================================================
            // 4. CLB Cầu Lông Bách Khoa - Xây Dựng (Host: user3)
            // Sinh hoạt: Chiều Thứ 3 & Chiều Thứ 5 (16:00 - 18:00)
            // =========================================================================
            var club4 = new ClubGroup(
                groupName: "CLB Cầu Lông Bách Khoa - Xây Dựng",
                description: "Sân chơi thể thao cho sinh viên và cựu sinh viên Bách Khoa, Xây Dựng khu vực Giải Phóng - Hai Bà Trưng.",
                avatarUrl: "https://images.unsplash.com/photo-1517649763962-0c623266ddc0?w=300",
                fixedVenueName: "Nhà Thi Đấu Bách Khoa",
                fixedAddress: "40 Tạ Quang Bửu, Phường Bách Khoa, Quận Hai Bà Trưng, Hà Nội",
                lat: 21.0055m,
                lng: 105.8458m,
                minSkillLevel: SkillLevel.NB,
                maxSkillLevel: SkillLevel.TB,
                groupQualityScore: 4.60m,
                isRecruiting: true,
                isActive: true,
                code: "CLB-BKXD"
            );
            await dbContext.ClubGroups.AddAsync(club4, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var (c4Host, _, _, c4Member, _) = await CreateClubRolesAsync(club4, allPermissions, cancellationToken);
            dbContext.GroupMembers.AddRange(
                new GroupMember(club4.Id, user3.Id, c4Host.Id, "MONTHLY", DateTime.UtcNow.AddMonths(6), ClubMemberStatus.ACTIVE, "Thăng Long (Host)"),
                new GroupMember(club4.Id, user4.Id, c4Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(2), ClubMemberStatus.ACTIVE, "Tuấn Anh"),
                new GroupMember(club4.Id, user6.Id, c4Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Phương Thảo"),
                new GroupMember(club4.Id, user8.Id, c4Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Khánh Linh")
            );
            dbContext.GroupSchedules.AddRange(
                new GroupSchedule(club4.Id, 2, new TimeOnly(16, 0), new TimeOnly(18, 0), true),
                new GroupSchedule(club4.Id, 4, new TimeOnly(16, 0), new TimeOnly(18, 0), true)
            );

            dbContext.JoinRequests.Add(
                new JoinRequest(club4.Id, user1.Id, "Chào CLB Bách Khoa, mình cựu sinh viên K58 muốn tham gia sinh hoạt cùng anh em vào các buổi chiều tối!", JoinRequestStatus.PENDING)
            );

            // =========================================================================
            // 5. CLB Cầu Lông Hà Đông Smashers (Host: user5)
            // Sinh hoạt: Chiều Thứ 7 (14:00 - 17:00)
            // =========================================================================
            var club5 = new ClubGroup(
                groupName: "CLB Cầu Lông Hà Đông Smashers",
                description: "Nhóm tập hợp các tay vợt phong trào khu vực Hà Đông, Thanh Xuân. Giao lưu cọ xát nhiệt tình, tinh thần fair-play, bia cỏ sau trận.",
                avatarUrl: "https://images.unsplash.com/photo-1508214751196-bcfd4ca60f91?w=300",
                fixedVenueName: "Sân Cầu Lông Hà Trì",
                fixedAddress: "Đường Hà Trì, Phường Hà Cầu, Quận Hà Đông, Hà Nội",
                lat: 20.9635m,
                lng: 105.7782m,
                minSkillLevel: SkillLevel.TBK,
                maxSkillLevel: SkillLevel.K,
                groupQualityScore: 4.95m,
                isRecruiting: true,
                isActive: true,
                code: "CLB-HADONG"
            );
            await dbContext.ClubGroups.AddAsync(club5, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var (c5Host, _, _, c5Member, _) = await CreateClubRolesAsync(club5, allPermissions, cancellationToken);
            dbContext.GroupMembers.AddRange(
                new GroupMember(club5.Id, user5.Id, c5Host.Id, "MONTHLY", DateTime.UtcNow.AddMonths(12), ClubMemberStatus.ACTIVE, "Hoàng Minh (Host)"),
                new GroupMember(club5.Id, user3.Id, c5Member.Id, "MONTHLY", DateTime.UtcNow.AddMonths(3), ClubMemberStatus.ACTIVE, "Thăng Long"),
                new GroupMember(club5.Id, user7.Id, c5Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Quốc Bảo")
            );
            dbContext.GroupSchedules.AddRange(
                new GroupSchedule(club5.Id, 6, new TimeOnly(14, 0), new TimeOnly(17, 0), true)
            );

            // =========================================================================
            // 6. CLB Cầu Lông Thanh Xuân Vui Khỏe (Host: admin)
            // Sinh hoạt: Chiều Chủ Nhật (14:30 - 17:30)
            // =========================================================================
            var club6 = new ClubGroup(
                groupName: "CLB Cầu Lông Thanh Xuân Vui Khỏe",
                description: "Giao lưu rèn luyện sức khỏe cuối tuần cho người mới chơi hoặc gia đình. Không áp lực thành tích, có hỗ trợ chỉnh dáng đập cầu.",
                avatarUrl: "https://images.unsplash.com/photo-1461896836934-ffe607ba8211?w=300",
                fixedVenueName: "Nhà Thi Đấu Thanh Xuân",
                fixedAddress: "166 Khuất Duy Tiến, Nhân Chính, Quận Thanh Xuân, Hà Nội",
                lat: 20.9982m,
                lng: 105.7989m,
                minSkillLevel: SkillLevel.Y,
                maxSkillLevel: SkillLevel.TB,
                groupQualityScore: 4.40m,
                isRecruiting: true,
                isActive: true,
                code: "CLB-THANHXUAN"
            );
            await dbContext.ClubGroups.AddAsync(club6, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var (c6Host, _, _, c6Member, _) = await CreateClubRolesAsync(club6, allPermissions, cancellationToken);
            dbContext.GroupMembers.AddRange(
                new GroupMember(club6.Id, admin.Id, c6Host.Id, "MONTHLY", DateTime.UtcNow.AddMonths(12), ClubMemberStatus.ACTIVE, "Tuấn Dũng (Host)"),
                new GroupMember(club6.Id, user6.Id, c6Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Phương Thảo"),
                new GroupMember(club6.Id, user8.Id, c6Member.Id, "DAILY", null, ClubMemberStatus.ACTIVE, "Khánh Linh")
            );
            dbContext.GroupSchedules.AddRange(
                new GroupSchedule(club6.Id, 0, new TimeOnly(14, 30), new TimeOnly(17, 30), true)
            );

            // =========================================================================
            // SESSIONS TIMELINE (Lịch sử từ đầu tháng 10/2026 đến hôm nay & tương lai)
            // Hoàn toàn không chồng chéo khung giờ chơi của các thành viên!
            // =========================================================================

            // Tuần 1: Thứ 3 ngày 06/10/2026 (CLB Cầu Giấy 18:30 - 21:00) - COMPLETED
            var dOct06 = new DateTime(2026, 10, 6, 11, 30, 0, DateTimeKind.Utc); // 18:30 VN
            var sOct06 = new BadmintonSession(
                club1.Id,
                "Buổi tối thứ 3 giao lưu khai sân tháng 10",
                dOct06,
                dOct06.AddHours(2.5),
                "Sân Cầu Lông NTĐ Cầu Giấy, 35 Trần Quý Kiên, Cầu Giấy, Hà Nội",
                courtCount: 2,
                maxPlayers: 12,
                courtRentalCost: 350000,
                status: SessionStatus.COMPLETED
            );
            sOct06.CompleteAttendance(user1.Id, dOct06);
            await dbContext.BadmintonSessions.AddAsync(sOct06, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.SessionBookings.AddRange(
                CreateCheckedInBooking(sOct06.Id, user1.Id, user1.Id, dOct06),
                CreateCheckedInBooking(sOct06.Id, user2.Id, user1.Id, dOct06),
                CreateCheckedInBooking(sOct06.Id, user3.Id, user1.Id, dOct06),
                CreateCheckedInBooking(sOct06.Id, user4.Id, user1.Id, dOct06),
                CreateCheckedInBooking(sOct06.Id, user5.Id, user1.Id, dOct06)
            );
            dbContext.SessionFees.AddRange(
                new SessionFee(sOct06.Id, user1.Id, 40000, PriceType.DAILY_MEMBER, "FEE-HN-1001", "vietqr-payload-1", PaymentMethod.BANK_TRANSFER, paymentStatus: PaymentStatus.PAID),
                new SessionFee(sOct06.Id, user2.Id, 35000, PriceType.DAILY_MEMBER, "FEE-HN-1002", "vietqr-payload-2", PaymentMethod.BANK_TRANSFER, discountApplied: 5000, paymentStatus: PaymentStatus.PAID),
                new SessionFee(sOct06.Id, user3.Id, 40000, PriceType.DAILY_MEMBER, "FEE-HN-1003", "vietqr-payload-3", PaymentMethod.BANK_TRANSFER, paymentStatus: PaymentStatus.PAID),
                new SessionFee(sOct06.Id, user4.Id, 40000, PriceType.DAILY_MEMBER, "FEE-HN-1004", "vietqr-payload-4", PaymentMethod.CASH, paymentStatus: PaymentStatus.PAID),
                new SessionFee(sOct06.Id, user5.Id, 40000, PriceType.DAILY_MEMBER, "FEE-HN-1005", "vietqr-payload-5", PaymentMethod.BANK_TRANSFER, paymentStatus: PaymentStatus.PAID)
            );

            // Tuần 2: Thứ 5 ngày 15/10/2026 (CLB Cầu Giấy 18:30 - 21:00) - COMPLETED
            var dOct15 = new DateTime(2026, 10, 15, 11, 30, 0, DateTimeKind.Utc);
            var sOct15 = new BadmintonSession(
                club1.Id,
                "Buổi tối thứ 5 cọ xát nâng cao thể lực",
                dOct15,
                dOct15.AddHours(2.5),
                "Sân Cầu Lông NTĐ Cầu Giấy, 35 Trần Quý Kiên, Cầu Giấy, Hà Nội",
                courtCount: 2,
                maxPlayers: 12,
                courtRentalCost: 350000,
                status: SessionStatus.COMPLETED
            );
            sOct15.CompleteAttendance(user1.Id, dOct15);
            await dbContext.BadmintonSessions.AddAsync(sOct15, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.SessionBookings.AddRange(
                CreateCheckedInBooking(sOct15.Id, user1.Id, user1.Id, dOct15),
                CreateCheckedInBooking(sOct15.Id, user2.Id, user1.Id, dOct15),
                CreateCheckedInBooking(sOct15.Id, user4.Id, user1.Id, dOct15),
                CreateCheckedInBooking(sOct15.Id, user6.Id, user1.Id, dOct15),
                CreateCheckedInBooking(sOct15.Id, user7.Id, user1.Id, dOct15)
            );
            dbContext.SessionFees.AddRange(
                new SessionFee(sOct15.Id, user1.Id, 40000, PriceType.DAILY_MEMBER, "FEE-HN-1006", "vietqr-payload-6", PaymentMethod.BANK_TRANSFER, paymentStatus: PaymentStatus.PAID),
                new SessionFee(sOct15.Id, user2.Id, 35000, PriceType.DAILY_MEMBER, "FEE-HN-1007", "vietqr-payload-7", PaymentMethod.BANK_TRANSFER, discountApplied: 5000, paymentStatus: PaymentStatus.PAID),
                new SessionFee(sOct15.Id, user4.Id, 40000, PriceType.DAILY_MEMBER, "FEE-HN-1008", "vietqr-payload-8", PaymentMethod.CASH, paymentStatus: PaymentStatus.PAID),
                new SessionFee(sOct15.Id, user6.Id, 35000, PriceType.DAILY_MEMBER, "FEE-HN-1009", "vietqr-payload-9", PaymentMethod.BANK_TRANSFER, discountApplied: 5000, paymentStatus: PaymentStatus.PAID),
                new SessionFee(sOct15.Id, user7.Id, 40000, PriceType.DAILY_MEMBER, "FEE-HN-1010", "vietqr-payload-10", PaymentMethod.BANK_TRANSFER, paymentStatus: PaymentStatus.PAID)
            );

            // Tuần 3: Thứ 4 ngày 21/10/2026 (CLB Đống Đa 19:30 - 22:00) - COMPLETED
            var dOct21 = new DateTime(2026, 10, 21, 12, 30, 0, DateTimeKind.Utc);
            var sOct21 = new BadmintonSession(
                club2.Id,
                "Buổi tối thứ 4 giao hữu câu lạc bộ Đống Đa",
                dOct21,
                dOct21.AddHours(2.5),
                "Nhà Thi Đấu Trịnh Hoài Đức, 12 Trịnh Hoài Đức, Đống Đa, Hà Nội",
                courtCount: 2,
                maxPlayers: 12,
                courtRentalCost: 360000,
                status: SessionStatus.COMPLETED
            );
            sOct21.CompleteAttendance(admin.Id, dOct21);
            await dbContext.BadmintonSessions.AddAsync(sOct21, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.SessionBookings.AddRange(
                CreateCheckedInBooking(sOct21.Id, admin.Id, admin.Id, dOct21),
                CreateCheckedInBooking(sOct21.Id, user1.Id, admin.Id, dOct21),
                CreateCheckedInBooking(sOct21.Id, user2.Id, admin.Id, dOct21),
                CreateCheckedInBooking(sOct21.Id, user4.Id, admin.Id, dOct21),
                CreateCheckedInBooking(sOct21.Id, user5.Id, admin.Id, dOct21)
            );
            dbContext.SessionFees.AddRange(
                new SessionFee(sOct21.Id, admin.Id, 45000, PriceType.DAILY_MEMBER, "FEE-HN-1011", "vietqr-payload-11", PaymentMethod.BANK_TRANSFER, paymentStatus: PaymentStatus.PAID),
                new SessionFee(sOct21.Id, user1.Id, 45000, PriceType.DAILY_MEMBER, "FEE-HN-1012", "vietqr-payload-12", PaymentMethod.BANK_TRANSFER, paymentStatus: PaymentStatus.PAID),
                new SessionFee(sOct21.Id, user2.Id, 40000, PriceType.DAILY_MEMBER, "FEE-HN-1013", "vietqr-payload-13", PaymentMethod.BANK_TRANSFER, discountApplied: 5000, paymentStatus: PaymentStatus.PAID),
                new SessionFee(sOct21.Id, user4.Id, 45000, PriceType.DAILY_MEMBER, "FEE-HN-1014", "vietqr-payload-14", PaymentMethod.CASH, paymentStatus: PaymentStatus.PAID),
                new SessionFee(sOct21.Id, user5.Id, 45000, PriceType.DAILY_MEMBER, "FEE-HN-1015", "vietqr-payload-15", PaymentMethod.BANK_TRANSFER, paymentStatus: PaymentStatus.PAID)
            );

            // Hôm nay: Thứ 2 ngày 26/10/2026 - Tối (CLB Đống Đa 19:30 - 22:00) - Đang diễn ra (PLAYING)
            var dToday = new DateTime(2026, 10, 26, 12, 30, 0, DateTimeKind.Utc); // 19:30 VN
            var sToday = new BadmintonSession(
                club2.Id,
                "Buổi tối thứ 2 rèn luyện sức bền đầu tuần",
                dToday,
                dToday.AddHours(2.5),
                "Nhà Thi Đấu Trịnh Hoài Đức, 12 Trịnh Hoài Đức, Đống Đa, Hà Nội",
                courtCount: 2,
                maxPlayers: 12,
                courtRentalCost: 360000,
                status: SessionStatus.PLAYING
            );
            await dbContext.BadmintonSessions.AddAsync(sToday, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var bAdmin = CreateCheckedInBooking(sToday.Id, admin.Id, admin.Id, dToday);
            var bU1 = CreateCheckedInBooking(sToday.Id, user1.Id, admin.Id, dToday);
            var bU2 = CreateCheckedInBooking(sToday.Id, user2.Id, admin.Id, dToday);
            var bU5 = CreateCheckedInBooking(sToday.Id, user5.Id, admin.Id, dToday);
            sToday.CompleteAttendance(admin.Id, dToday);
            dbContext.SessionBookings.AddRange(bAdmin, bU1, bU2, bU5,
                new SessionBooking(sToday.Id, user4.Id, null, BookingStatus.CONFIRMED, 0));

            // Ngày mai: Thứ 3 ngày 27/10/2026 - Chiều (CLB Bách Khoa 16:00 - 18:00) - OPEN (Khóa đăng ký gần kề)
            var dTueBk = new DateTime(2026, 10, 27, 9, 0, 0, DateTimeKind.Utc); // 16:00 VN
            var sTueBk = new BadmintonSession(
                club4.Id,
                "Buổi chiều thứ 3 sinh viên Bách Khoa giao lưu",
                dTueBk,
                dTueBk.AddHours(2),
                "Nhà Thi Đấu Bách Khoa, 40 Tạ Quang Bửu, Hai Bà Trưng, Hà Nội",
                courtCount: 1,
                targetPlayerPerCourt: 6,
                maxPlayers: 6,
                courtRentalCost: 200000,
                deadlineBooking: dTueBk.AddMinutes(-30),
                status: SessionStatus.OPEN
            );
            await dbContext.BadmintonSessions.AddAsync(sTueBk, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.SessionBookings.AddRange(
                new SessionBooking(sTueBk.Id, user3.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sTueBk.Id, user4.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sTueBk.Id, user6.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sTueBk.Id, user8.Id, null, BookingStatus.CONFIRMED, 0)
            );

            // Ngày mai: Thứ 3 ngày 27/10/2026 - Tối (CLB Cầu Giấy 18:30 - 21:00) - OPEN (Đầy chỗ, có danh sách chờ)
            // Lưu ý: user1, user2, user5 tham gia buổi tối Cầu Giấy (18:30) KHÔNG hề trùng với buổi chiều Bách Khoa (16:00-18:00)!
            var dTueCg = new DateTime(2026, 10, 27, 11, 30, 0, DateTimeKind.Utc); // 18:30 VN
            var sTueCg = new BadmintonSession(
                club1.Id,
                "Buổi tối thứ 3 cọ xát ghép cặp thi đấu",
                dTueCg,
                dTueCg.AddHours(2.5),
                "Sân Cầu Lông NTĐ Cầu Giấy, 35 Trần Quý Kiên, Cầu Giấy, Hà Nội",
                courtCount: 1,
                targetPlayerPerCourt: 6,
                maxPlayers: 6,
                courtRentalCost: 240000,
                status: SessionStatus.OPEN
            );
            await dbContext.BadmintonSessions.AddAsync(sTueCg, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.SessionBookings.AddRange(
                new SessionBooking(sTueCg.Id, user1.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sTueCg.Id, user2.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sTueCg.Id, user5.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sTueCg.Id, user6.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sTueCg.Id, user7.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sTueCg.Id, user4.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sTueCg.Id, user8.Id, null, BookingStatus.WAITING, 1)
            );

            // Ngày kia: Thứ 4 ngày 28/10/2026 - Tối (CLB Đống Đa 19:30 - 22:00) - OPEN có tuyển vãng lai
            var dWedDd = new DateTime(2026, 10, 28, 12, 30, 0, DateTimeKind.Utc); // 19:30 VN
            var sWedDd = new BadmintonSession(
                club2.Id,
                "Buổi tối thứ 4 giao lưu mở rộng đón tiếp khách",
                dWedDd,
                dWedDd.AddHours(2.5),
                "Nhà Thi Đấu Trịnh Hoài Đức, 12 Trịnh Hoài Đức, Đống Đa, Hà Nội",
                courtCount: 2,
                targetPlayerPerCourt: 6,
                maxPlayers: 12,
                courtRentalCost: 360000,
                status: SessionStatus.OPEN
            );
            await dbContext.BadmintonSessions.AddAsync(sWedDd, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.SessionBookings.AddRange(
                new SessionBooking(sWedDd.Id, admin.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sWedDd.Id, user1.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sWedDd.Id, user2.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sWedDd.Id, user4.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sWedDd.Id, user5.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sWedDd.Id, user7.Id, null, BookingStatus.CONFIRMED, 0)
            );
            var rWedDd = new GuestRecruitment(sWedDd.Id, slotsNeeded: 4, minSkillLevel: SkillLevel.TB, sharedCost: 50000, scanRadiusKm: 8.0m, status: RecruitmentStatus.ACTIVE);
            dbContext.GuestRecruitments.Add(rWedDd);

            // Thứ 5 ngày 29/10/2026 - Tối (CLB Cầu Giấy 18:30 - 21:00) - OPEN có tuyển vãng lai
            var dThuCg = new DateTime(2026, 10, 29, 11, 30, 0, DateTimeKind.Utc); // 18:30 VN
            var sThuCg = new BadmintonSession(
                club1.Id,
                "Buổi tối thứ 5 rèn luyện kỹ chiến thuật đôi",
                dThuCg,
                dThuCg.AddHours(2.5),
                "Sân Cầu Lông NTĐ Cầu Giấy, 35 Trần Quý Kiên, Cầu Giấy, Hà Nội",
                courtCount: 2,
                targetPlayerPerCourt: 6,
                maxPlayers: 12,
                courtRentalCost: 360000,
                status: SessionStatus.OPEN
            );
            await dbContext.BadmintonSessions.AddAsync(sThuCg, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.SessionBookings.AddRange(
                new SessionBooking(sThuCg.Id, user1.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sThuCg.Id, user2.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sThuCg.Id, user3.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sThuCg.Id, user4.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sThuCg.Id, user5.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sThuCg.Id, user6.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sThuCg.Id, user7.Id, null, BookingStatus.CONFIRMED, 0)
            );
            var rThuCg = new GuestRecruitment(sThuCg.Id, slotsNeeded: 2, minSkillLevel: SkillLevel.TB, sharedCost: 50000, scanRadiusKm: 6.0m, status: RecruitmentStatus.ACTIVE);
            dbContext.GuestRecruitments.Add(rThuCg);

            // Cuối tuần: Thứ 7 ngày 31/10/2026 - Sáng (CLB Tây Hồ 07:30 - 10:30) - OPEN
            var dSatTh = new DateTime(2026, 10, 31, 0, 30, 0, DateTimeKind.Utc); // 07:30 VN
            var sSatTh = new BadmintonSession(
                club3.Id,
                "Buổi sáng thứ 7 không khí trong lành Hồ Tây",
                dSatTh,
                dSatTh.AddHours(3),
                "Sân Cầu Lông Tây Hồ, 101 Xuân Diệu, Phường Quảng An, Tây Hồ, Hà Nội",
                courtCount: 2,
                maxPlayers: 10,
                courtRentalCost: 300000,
                status: SessionStatus.OPEN
            );
            await dbContext.BadmintonSessions.AddAsync(sSatTh, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.SessionBookings.AddRange(
                new SessionBooking(sSatTh.Id, user2.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sSatTh.Id, user3.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sSatTh.Id, user5.Id, null, BookingStatus.CONFIRMED, 0),
                new SessionBooking(sSatTh.Id, user7.Id, null, BookingStatus.CONFIRMED, 0)
            );

            // =========================================================================
            // Sổ Quỹ Thực Tế CLB Cầu Giấy (Group Ledgers)
            // =========================================================================
            dbContext.GroupLedgers.AddRange(
                new GroupLedger(club1.Id, user1.Id, TransactionType.INFLOW, LedgerCategory.MONTHLY_FUND, 2450000, "Thu tiền quỹ tháng 10 của 7 thành viên cố định"),
                new GroupLedger(club1.Id, user1.Id, TransactionType.OUTFLOW, LedgerCategory.COURT_RENT, 700000, "Thanh toán tiền thuê sân buổi 06/10 và 15/10"),
                new GroupLedger(club1.Id, user1.Id, TransactionType.OUTFLOW, LedgerCategory.SHUTTLE_EXPENSE, 520000, "Mua 2 ống cầu Ba Sao Pro thi đấu"),
                new GroupLedger(club1.Id, user1.Id, TransactionType.INFLOW, LedgerCategory.SESSION_FEE, 195000, "Thu tiền sân buổi giao lưu ngày 06/10", sOct06.Id),
                new GroupLedger(club1.Id, user1.Id, TransactionType.INFLOW, LedgerCategory.SESSION_FEE, 190000, "Thu tiền sân buổi giao lưu ngày 15/10", sOct15.Id)
            );

            // Đánh Giá CLB (Reviews)
            dbContext.GroupReviews.AddRange(
                new GroupReview(user2.Id, club1.Id, 5, "Sân thoáng mát, anh em nhiệt tình", "Nhóm sinh hoạt rất đều, đúng giờ, mọi người chia sẻ kỹ thuật rất tận tâm!"),
                new GroupReview(user3.Id, club1.Id, 5, "Tổ chức rất tốt", "Host Dũng điều phối trận bài bản, cầu và nước đầy đủ chu đáo."),
                new GroupReview(user4.Id, club1.Id, 4, "Buổi chơi chất lượng", "Trình độ vừa tầm, đánh rất đã tay, sẽ tiếp tục gắn bó lâu dài!")
            );

            // Thông Báo (Notifications)
            dbContext.Notifications.AddRange(
                new Notification(user1.Id, "Đơn gia nhập nhóm", "Bạn đã được duyệt làm thành viên của CLB Cầu Lông Đống Đa Warriors", NotificationType.QUEUE),
                new Notification(user1.Id, "Nhắc nhở buổi chơi", "Buổi tối thứ 3 tại sân NTĐ Cầu Giấy sẽ bắt đầu lúc 18:30. Hãy đến khởi động đúng giờ nhé!", NotificationType.SESSION),
                new Notification(user1.Id, "Yêu cầu gia nhập mới", "Thành viên Bùi Khánh Linh vừa gửi đơn xin gia nhập CLB Cầu Lông Phong Trào Cầu Giấy", NotificationType.QUEUE),
                new Notification(admin.Id, "Thông báo hệ thống", "Hệ thống quản lý nhóm cầu lông LegendsTeamVN đã cập nhật phiên bản Hà Nội", NotificationType.SESSION)
            );

            dbContext.MemberReviews.Add(
                new MemberReview(user2.Id, user1.Id, 5, "Nhiệt tình", "Đánh đôi công thủ toàn diện, bọc lót đồng đội rất tốt.")
            );

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Clean, logical monthly timeline seeded successfully for Hanoi badminton clubs.");
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
