using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Persistence.Seeders;

public class BadmintonUserSeeder(
    BadmintonDbContext dbContext,
    UserManager<LegendsTeamVN.Core.Identity.Entities.AppUser> userManager,
    ILogger<BadmintonUserSeeder> logger) : IDataSeeder
{
    public int Order => 2;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Ensuring default Badminton domain users exist...");

        var defaultUsers = new (string UserName, string Email, string FullName, short Gender, SkillLevel SkillLevel, string? AvatarUrl, string? PhoneNumber)[]
        {
            ("admin", "admin@l7ungdz.id.vn", "Nguyễn Tuấn Dũng", 1, SkillLevel.K, "https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=150", "0909999999"),
            ("user1", "user1@l7ungdz.id.vn", "Trần Việt Dũng", 1, SkillLevel.TBK, "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=150", "0901234567"),
            ("user2", "user2@l7ungdz.id.vn", "Nguyễn Thị Phương Mai", 2, SkillLevel.TB, "https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=150", "0902345678"),
            ("user3", "user3@l7ungdz.id.vn", "Lê Thăng Long", 1, SkillLevel.K, "https://images.unsplash.com/photo-1570295999919-56ceb5ecca61?w=150", "0903456789"),
            ("user4", "user4@l7ungdz.id.vn", "Phạm Tuấn Anh", 1, SkillLevel.TBY, "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=150", "0904567890"),
            ("user5", "user5@l7ungdz.id.vn", "Vũ Hoàng Minh", 1, SkillLevel.K, "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=150", "0905678901"),
            ("user6", "user6@l7ungdz.id.vn", "Đặng Phương Thảo", 2, SkillLevel.Y, "https://images.unsplash.com/photo-1438761681033-6461ffad8d80?w=150", "0906789012"),
            ("user7", "user7@l7ungdz.id.vn", "Hoàng Quốc Bảo", 1, SkillLevel.TB, "https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?w=150", "0907890123"),
            ("user8", "user8@l7ungdz.id.vn", "Bùi Khánh Linh", 2, SkillLevel.NB, "https://images.unsplash.com/photo-1544005313-94ddf0286df2?w=150", "0908901234")
        };

        foreach (var info in defaultUsers)
        {
            var identityUser = await userManager.FindByEmailAsync(info.Email) 
                            ?? await userManager.FindByNameAsync(info.UserName);

            if (identityUser != null)
            {
                var domainUser = await dbContext.AppUsers.FirstOrDefaultAsync(u => u.Id == identityUser.Id, cancellationToken);
                if (domainUser == null)
                {
                    domainUser = AppUser.CreateStandard(
                        id: identityUser.Id,
                        fullName: info.FullName,
                        emailAddress: info.Email,
                        phoneNumber: info.PhoneNumber,
                        gender: info.Gender,
                        skillLevel: info.SkillLevel
                    );
                    domainUser.UpdateProfile(info.FullName, info.Gender, info.AvatarUrl, info.SkillLevel, true);
                    await dbContext.AppUsers.AddAsync(domainUser, cancellationToken);
                }
                else
                {
                    domainUser.UpdateProfile(info.FullName, info.Gender, info.AvatarUrl, info.SkillLevel, true);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Badminton domain users verified with clean natural profiles.");
    }
}
