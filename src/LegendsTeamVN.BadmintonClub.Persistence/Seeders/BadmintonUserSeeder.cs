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
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Ensuring default Badminton domain users exist...");

        var defaultUsers = new (string UserName, string Email, string FullName)[]
        {
            ("user1", "user1@l7ungdz.id.vn", "Người Dùng 1"),
            ("user2", "user2@l7ungdz.id.vn", "Người Dùng 2"),
            ("user3", "user3@l7ungdz.id.vn", "Người Dùng 3")
        };

        foreach (var info in defaultUsers)
        {
            var identityUser = await userManager.FindByEmailAsync(info.Email) 
                            ?? await userManager.FindByNameAsync(info.UserName);

            if (identityUser != null)
            {
                var existsInDomain = await dbContext.AppUsers.AnyAsync(u => u.Id == identityUser.Id, cancellationToken);
                if (!existsInDomain)
                {
                    var domainUser = AppUser.CreateStandard(
                        id: identityUser.Id,
                        fullName: info.FullName,
                        emailAddress: info.Email,
                        phoneNumber: null,
                        gender: 0,
                        skillLevel: SkillLevel.NB
                    );

                    await dbContext.AppUsers.AddAsync(domainUser, cancellationToken);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Badminton domain users verified.");
    }
}
