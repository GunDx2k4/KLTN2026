using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.GetMyClubs;

public sealed class GetMyClubsQueryHandler(
    IClubMemberRepository clubMemberRepository,
    ICurrentUserService currentUserService) : IQueryHandler<GetMyClubsQuery, List<MyClubResponse>>
{
    public async Task<Result<List<MyClubResponse>>> Handle(GetMyClubsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<List<MyClubResponse>>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để xem danh sách nhóm của bạn."));
        }

        var memberships = await clubMemberRepository.FindAll(m => m.UserId == userId.Value, m => m.Club)
            .OrderByDescending(m => m.JoinedAt)
            .Select(m => new MyClubResponse(
                m.ClubId,
                m.Club.Name,
                m.Club.Code,
                m.Club.Description,
                m.Club.AvatarUrl,
                m.Role,
                m.Status,
                m.JoinedAt
            ))
            .ToListAsync(cancellationToken);

        return Result.Success(memberships);
    }
}
