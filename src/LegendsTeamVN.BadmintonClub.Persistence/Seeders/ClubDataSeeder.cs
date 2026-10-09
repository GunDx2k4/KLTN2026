using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Data;
using LegendsTeamVN.Core.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Persistence.Seeders;

public class ClubDataSeeder(
    BadmintonDbContext dbContext,
    UserManager<AppUser> userManager,
    ILogger<ClubDataSeeder> logger) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Ensuring test Badminton Clubs and memberships...");

        var adminUser = await userManager.FindByNameAsync("admin") ?? await userManager.FindByEmailAsync("admin@admin.com");
        var adminId = adminUser?.Id ?? Guid.Parse("01a11150-3663-7696-9b9e-fdfb79c60dc7");

        var namUser = await userManager.FindByNameAsync("hoang_nam");
        var namId = namUser?.Id ?? Guid.Parse("22222222-2222-2222-2222-222222222222");

        var linhUser = await userManager.FindByNameAsync("thuy_linh");
        var linhId = linhUser?.Id ?? Guid.Parse("33333333-3333-3333-3333-333333333333");

        var baoUser = await userManager.FindByNameAsync("quoc_bao");
        var baoId = baoUser?.Id ?? Guid.Parse("44444444-4444-4444-4444-444444444444");

        var anhUser = await userManager.FindByNameAsync("minh_anh");
        var anhId = anhUser?.Id ?? Guid.Parse("55555555-5555-5555-5555-555555555555");

        var kietUser = await userManager.FindByNameAsync("tuan_kiet");
        var kietId = kietUser?.Id ?? Guid.Parse("66666666-6666-6666-6666-666666666666");

        var huongUser = await userManager.FindByNameAsync("mai_huong");
        var huongId = huongUser?.Id ?? Guid.Parse("77777777-7777-7777-7777-777777777777");

        // 1. Seed Clubs
        var clubsDefinition = new[]
        {
            (
                Id: Guid.Parse("522d785c-20ab-4b30-929c-efa5e79e633d"),
                Name: "CLB Cầu Lông Huyền Thoại",
                Code: "LEGENDS-Q1",
                Desc: "Câu lạc bộ cầu lông Legend Team VN - Chi nhánh Quận 1",
                Avatar: "https://images.unsplash.com/photo-1626224583764-f87db24ac4ea"
            ),
            (
                Id: Guid.Parse("55288de6-9bf2-4349-b8c9-79dbcdbe043c"),
                Name: "CLB Cầu Lông Chiến Binh",
                Code: "WARRIORS-BT",
                Desc: "Nhóm tập luyện thể lực và giao lưu cầu lông cuối tuần",
                Avatar: "https://images.unsplash.com/photo-1544919982-b61976f0ba43"
            ),
            (
                Id: Guid.Parse("77778888-1111-2222-3333-444455556666"),
                Name: "CLB Giao Lưu Cầu Lông Thủ Đức",
                Code: "CHALLENGER-TD",
                Desc: "Nhóm giao lưu kết nối thành viên và khách vãng lai khu vực TP. Thủ Đức",
                Avatar: "https://images.unsplash.com/photo-1511067007772-9da28a00e82f"
            ),
            (
                Id: Guid.Parse("88889999-2222-3333-4444-555566667777"),
                Name: "CLB Cầu Lông Smash Pro Quận 7",
                Code: "SMASH-Q7",
                Desc: "Hội cầu lông phong trào Phú Mỹ Hưng, trình độ trung bình - khá, giao lưu hàng tuần",
                Avatar: "https://images.unsplash.com/photo-1521537634581-0dced2fee2ef"
            ),
            (
                Id: Guid.Parse("99990000-3333-4444-5555-666677778888"),
                Name: "CLB Cầu Lông Phoenix Gò Vấp",
                Code: "PHOENIX-GV",
                Desc: "Sân chơi vui vẻ, nâng cao sức khỏe, kết nối anh em đam mê cầu lông Gò Vấp",
                Avatar: "https://images.unsplash.com/photo-1517649763962-0c623266ddc0"
            )
        };

        foreach (var def in clubsDefinition)
        {
            var existingClub = await dbContext.Clubs.FirstOrDefaultAsync(c => c.Code == def.Code, cancellationToken);
            if (existingClub == null)
            {
                var newClub = new Club(def.Name, def.Code, def.Desc, def.Avatar);
                typeof(Club).GetProperty("Id")?.SetValue(newClub, def.Id);
                dbContext.Clubs.Add(newClub);
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        // 2. Seed Memberships (FR03: Independent roles across clubs)
        var memberships = new (Guid ClubId, Guid UserId, ClubRole Role, string Nickname)[]
        {
            // Admin user multi-group roles
            (Guid.Parse("522d785c-20ab-4b30-929c-efa5e79e633d"), adminId, ClubRole.Host, "Admin (Chủ phòng)"),
            (Guid.Parse("55288de6-9bf2-4349-b8c9-79dbcdbe043c"), adminId, ClubRole.Treasurer, "Admin (Thủ quỹ)"),
            (Guid.Parse("77778888-1111-2222-3333-444455556666"), adminId, ClubRole.Guest, "Admin (Khách vãng lai)"),
            (Guid.Parse("88889999-2222-3333-4444-555566667777"), adminId, ClubRole.Member, "Admin (Thành viên)"),

            // Club 1 (LEGENDS-Q1)
            (Guid.Parse("522d785c-20ab-4b30-929c-efa5e79e633d"), linhId, ClubRole.Treasurer, "Linh Thủ Quỹ"),
            (Guid.Parse("522d785c-20ab-4b30-929c-efa5e79e633d"), namId, ClubRole.Member, "Nam Smash"),
            (Guid.Parse("522d785c-20ab-4b30-929c-efa5e79e633d"), baoId, ClubRole.Member, "Bảo Phản Tạt"),
            (Guid.Parse("522d785c-20ab-4b30-929c-efa5e79e633d"), kietId, ClubRole.Guest, "Kiệt Vãng Lai"),

            // Club 2 (WARRIORS-BT)
            (Guid.Parse("55288de6-9bf2-4349-b8c9-79dbcdbe043c"), namId, ClubRole.Host, "Nam Chủ CLB"),
            (Guid.Parse("55288de6-9bf2-4349-b8c9-79dbcdbe043c"), anhId, ClubRole.Member, "Ánh Lưới"),
            (Guid.Parse("55288de6-9bf2-4349-b8c9-79dbcdbe043c"), huongId, ClubRole.Member, "Hương Bền Bỉ"),

            // Club 3 (CHALLENGER-TD)
            (Guid.Parse("77778888-1111-2222-3333-444455556666"), baoId, ClubRole.Host, "Bảo Trưởng Nhóm"),
            (Guid.Parse("77778888-1111-2222-3333-444455556666"), anhId, ClubRole.Treasurer, "Ánh Kế Toán"),
            (Guid.Parse("77778888-1111-2222-3333-444455556666"), kietId, ClubRole.Member, "Kiệt Tấn Công"),
            (Guid.Parse("77778888-1111-2222-3333-444455556666"), huongId, ClubRole.Guest, "Hương Khách Mời"),

            // Club 4 (SMASH-Q7)
            (Guid.Parse("88889999-2222-3333-4444-555566667777"), kietId, ClubRole.Host, "Kiệt Smash Host"),
            (Guid.Parse("88889999-2222-3333-4444-555566667777"), linhId, ClubRole.Member, "Linh Cầu Đỏ"),

            // Club 5 (PHOENIX-GV)
            (Guid.Parse("99990000-3333-4444-5555-666677778888"), huongId, ClubRole.Host, "Hương Phoenix"),
            (Guid.Parse("99990000-3333-4444-5555-666677778888"), namId, ClubRole.Member, "Nam Gò Vấp")
        };

        foreach (var m in memberships)
        {
            var exists = await dbContext.ClubMembers.AnyAsync(
                x => x.ClubId == m.ClubId && x.UserId == m.UserId,
                cancellationToken
            );

            if (!exists)
            {
                var newMember = new ClubMember(m.ClubId, m.UserId, m.Role, ClubMemberStatus.Active, m.Nickname);
                dbContext.ClubMembers.Add(newMember);
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        // 3. Seed Sample Sessions
        if (!await dbContext.ClubSessions.AnyAsync(s => s.Title.Contains("kỹ thuật"), cancellationToken))
        {
            var sessions = new[]
            {
                new ClubSession(
                    Guid.Parse("522d785c-20ab-4b30-929c-efa5e79e633d"),
                    "Kèo kỹ thuật & rèn thể lực tối Thứ 3",
                    DateTime.UtcNow.Date.AddDays(2).AddHours(19),
                    DateTime.UtcNow.Date.AddDays(2).AddHours(21),
                    "Sân Cầu Lông Lan Anh, CMT8, Quận 10",
                    60000,
                    8
                ),
                new ClubSession(
                    Guid.Parse("522d785c-20ab-4b30-929c-efa5e79e633d"),
                    "Giao lưu nội bộ CLB sáng Chủ Nhật",
                    DateTime.UtcNow.Date.AddDays(4).AddHours(8),
                    DateTime.UtcNow.Date.AddDays(4).AddHours(11),
                    "Sân Cầu Lông Kỳ Hòa, Quận 10",
                    55000,
                    16
                ),
                new ClubSession(
                    Guid.Parse("55288de6-9bf2-4349-b8c9-79dbcdbe043c"),
                    "Chiến Binh tập thể lực tối Thứ 5",
                    DateTime.UtcNow.Date.AddDays(1).AddHours(18).AddMinutes(30),
                    DateTime.UtcNow.Date.AddDays(1).AddHours(21).AddMinutes(30),
                    "Sân Cầu Lông Bình Triệu, Bình Thạnh",
                    45000,
                    10
                ),
                new ClubSession(
                    Guid.Parse("77778888-1111-2222-3333-444455556666"),
                    "Kèo giao lưu thành viên & vãng lai tối Thứ 6",
                    DateTime.UtcNow.Date.AddDays(3).AddHours(18),
                    DateTime.UtcNow.Date.AddDays(3).AddHours(21),
                    "Sân Cầu Lông Trường Thọ, TP. Thủ Đức",
                    40000,
                    16
                ),
                new ClubSession(
                    Guid.Parse("88889999-2222-3333-4444-555566667777"),
                    "Smash Pro giao lưu tốc độ tối Thứ 4",
                    DateTime.UtcNow.Date.AddDays(1).AddHours(19),
                    DateTime.UtcNow.Date.AddDays(1).AddHours(22),
                    "Sân Tada Badminton, Quận 7",
                    70000,
                    12
                ),
                new ClubSession(
                    Guid.Parse("99990000-3333-4444-5555-666677778888"),
                    "Phoenix kết nối giao lưu tối Thứ 7",
                    DateTime.UtcNow.Date.AddDays(3).AddHours(18),
                    DateTime.UtcNow.Date.AddDays(3).AddHours(21),
                    "Sân Cầu Lông Tre Xanh, Gò Vấp",
                    50000,
                    12
                )
            };

            dbContext.ClubSessions.AddRange(sessions);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Test Badminton Clubs and memberships verified.");
    }
}
