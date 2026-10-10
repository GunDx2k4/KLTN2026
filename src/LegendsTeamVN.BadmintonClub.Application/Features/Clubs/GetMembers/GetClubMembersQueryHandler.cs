using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.GetMembers;

public sealed class GetClubMembersQueryHandler(
    IClubRepository clubRepository,
    IClubMemberRepository clubMemberRepository,
    IUserManagerService userManagerService) : IQueryHandler<GetClubMembersQuery, List<ClubMemberResponse>>
{
    public async Task<Result<List<ClubMemberResponse>>> Handle(GetClubMembersQuery request, CancellationToken cancellationToken)
    {
        var clubExists = await clubRepository.FindAll(c => c.Id == request.ClubId)
            .AnyAsync(cancellationToken);

        if (!clubExists)
        {
            return Result.Failure<List<ClubMemberResponse>>(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ."));
        }

        var members = await clubMemberRepository.FindAll(m => m.GroupId == request.ClubId, m => m.RoleEntity)
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(cancellationToken);

        var userIds = members.Select(m => m.UserId).Distinct().ToList();
        var users = await userManagerService.GetUsersQueryable()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.Email })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var result = members.Select(m =>
        {
            users.TryGetValue(m.UserId, out var user);
            return new ClubMemberResponse(
                Id: m.Id,
                ClubId: m.GroupId,
                UserId: m.UserId,
                UserName: user?.UserName,
                Email: user?.Email,
                RoleId: m.RoleId,
                RoleName: m.RoleEntity?.RoleName,
                Status: m.Status,
                Nickname: m.Nickname,
                JoinedAt: m.JoinedAt
            );
        }).ToList();

        return Result.Success(result);
    }
}
