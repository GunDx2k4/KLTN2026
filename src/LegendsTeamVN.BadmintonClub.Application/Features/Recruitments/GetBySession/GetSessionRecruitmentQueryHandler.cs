using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.GetBySession;

public sealed class GetSessionRecruitmentQueryHandler(
    IRecruitmentRepository recruitmentRepository,
    IBookingRepository bookingRepository) : IQueryHandler<GetSessionRecruitmentQuery, GuestRecruitmentResponse?>
{
    public async Task<Result<GuestRecruitmentResponse?>> Handle(GetSessionRecruitmentQuery request, CancellationToken cancellationToken)
    {
        var recruitment = await recruitmentRepository.FindAll(r => r.SessionId == request.SessionId && r.Status == RecruitmentStatus.ACTIVE)
            .Include(r => r.Session)
                .ThenInclude(s => s.Group)
            .OrderByDescending(r => r.BroadcastAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (recruitment is null)
        {
            return Result.Success<GuestRecruitmentResponse?>(null);
        }

        var confirmedCount = await bookingRepository.FindAll(b => b.RecruitmentId == recruitment.Id && b.Status == BookingStatus.CONFIRMED)
            .CountAsync(cancellationToken);

        var slotsRemaining = Math.Max(0, recruitment.SlotsNeeded - confirmedCount);

        var response = new GuestRecruitmentResponse(
            recruitment.Id,
            recruitment.SessionId,
            recruitment.Session?.Group?.GroupName ?? string.Empty,
            recruitment.Session?.CourtLocation ?? string.Empty,
            recruitment.Session?.StartTime ?? DateTime.MinValue,
            recruitment.Session?.EndTime ?? DateTime.MinValue,
            recruitment.SlotsNeeded,
            slotsRemaining,
            recruitment.MinSkillLevel,
            recruitment.SharedCost,
            recruitment.ScanRadiusKm,
            recruitment.Status,
            recruitment.BroadcastAt
        );

        return Result.Success<GuestRecruitmentResponse?>(response);
    }
}
