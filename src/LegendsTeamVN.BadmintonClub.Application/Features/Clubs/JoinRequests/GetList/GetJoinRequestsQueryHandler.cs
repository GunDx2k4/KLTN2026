using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.GetList;

public sealed class GetJoinRequestsQueryHandler(
    IJoinRequestRepository joinRequestRepository) : IQueryHandler<GetJoinRequestsQuery, List<JoinRequestResponse>>
{
    public async Task<Result<List<JoinRequestResponse>>> Handle(GetJoinRequestsQuery request, CancellationToken cancellationToken)
    {
        var query = joinRequestRepository.FindAll(
            r => r.GroupId == request.ClubId && (request.Status == null || r.Status == request.Status),
            r => r.User
        );

        var list = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new JoinRequestResponse(
                r.Id,
                r.GroupId,
                r.UserId,
                r.User.FullName,
                r.User.EmailAddress,
                r.User.PhoneNumber,
                r.User.AvatarUrl,
                r.Introduction,
                r.Status,
                r.CreatedAt,
                r.ReviewedAt,
                r.ApprovedBy
            ))
            .ToListAsync(cancellationToken);

        return Result.Success(list);
    }
}
