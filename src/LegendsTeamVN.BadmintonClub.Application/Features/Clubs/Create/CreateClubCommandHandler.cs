using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Create;

public sealed class CreateClubCommandHandler(
    IClubRepository clubRepository,
    ICurrentUserService currentUserService) : ICommandHandler<CreateClubCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateClubCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<Guid>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để tạo câu lạc bộ."));
        }

        var code = "CLB-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var club = new ClubGroup(
            groupName: request.Name,
            description: request.Description,
            avatarUrl: request.AvatarUrl,
            fixedVenueName: request.FixedVenueName,
            fixedAddress: request.FixedAddress,
            lat: request.Lat,
            lng: request.Lng,
            minSkillLevel: request.MinSkillLevel,
            maxSkillLevel: request.MaxSkillLevel,
            code: code
        );

        var allPermissions = await clubRepository.GetAllPermissionsAsync(cancellationToken);
        club.InitializeHostRole(allPermissions, userId.Value);

        if (request.Schedules != null && request.Schedules.Count > 0)
        {
            foreach (var s in request.Schedules)
            {
                club.Schedules.Add(new GroupSchedule(club.Id, s.DayOfWeek, s.StartTime, s.EndTime));
            }
        }

        // Khởi tạo quy tắc tính phí mặc định (Dynamic Fee Mode, làm tròn 1.000 VNĐ)
        club.FeeRules.Add(new GroupFeeRule(club.Id, FeeMode.DYNAMIC, roundRule: 1000));

        clubRepository.Add(club);

        return Result.Success(club.Id);
    }
}
