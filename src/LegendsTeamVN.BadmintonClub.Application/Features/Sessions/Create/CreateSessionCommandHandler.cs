using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Create;

public sealed class CreateSessionCommandHandler(
    ISessionRepository sessionRepository,
    IClubRepository clubRepository) : ICommandHandler<CreateSessionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        var club = await clubRepository.FindByIdAsync(request.ClubId, cancellationToken);
        if (club is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ."));
        }

        var req = request.Request;
        var maxPlayers = req.MaxPlayers ?? (req.CourtCount * req.TargetPlayerPerCourt);
        var deadline = req.DeadlineBooking ?? req.StartTime.AddHours(-2);

        var session = new BadmintonSession(
            groupId: request.ClubId,
            title: req.Title,
            startTime: req.StartTime,
            endTime: req.EndTime,
            courtLocation: req.CourtLocation,
            courtCount: req.CourtCount,
            targetPlayerPerCourt: req.TargetPlayerPerCourt,
            maxPlayers: maxPlayers,
            deadlineBooking: deadline,
            courtRentalCost: req.CourtRentalCost,
            status: SessionStatus.OPEN
        );

        sessionRepository.Add(session);

        return Result.Success(session.Id);
    }
}
