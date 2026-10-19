using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Create;

public sealed class CreateRecruitmentCommandHandler(
    ISessionRepository sessionRepository,
    IRecruitmentRepository recruitmentRepository) : ICommandHandler<CreateRecruitmentCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateRecruitmentCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetAttendanceAsync(request.SessionId, true, cancellationToken);
        if (session is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        }

        if (session.Status != SessionStatus.OPEN || DateTime.UtcNow >= session.EndTime)
        {
            return Result.Failure<Guid>(Error.Conflict("Session.RegistrationClosed", "Buổi chơi đã khóa hoặc hết giờ, không thể mở tin tuyển khách."));
        }

        // Hủy các bài đăng tìm khách đang active trước đó nếu có
        var activeRecruitments = session.Recruitments.Where(r => !r.IsDeleted &&
            r.Status is RecruitmentStatus.ACTIVE or RecruitmentStatus.FILLED).ToList();

        foreach (var active in activeRecruitments)
        {
            active.UpdateStatus(RecruitmentStatus.EXPIRED);
            recruitmentRepository.Update(active);
        }

        var req = request.Request;
        var recruitment = new GuestRecruitment(
            sessionId: request.SessionId,
            slotsNeeded: req.SlotsNeeded,
            minSkillLevel: req.MinSkillLevel,
            sharedCost: req.SharedCost,
            scanRadiusKm: req.ScanRadiusKm,
            status: RecruitmentStatus.ACTIVE
        );

        session.Recruitments.Add(recruitment);
        recruitmentRepository.Add(recruitment);
        RecruitmentRules.Synchronize(session, DateTime.UtcNow);

        return Result.Success(recruitment.Id);
    }
}
