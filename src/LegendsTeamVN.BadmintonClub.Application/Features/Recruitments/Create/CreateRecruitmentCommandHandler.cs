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
        var session = await sessionRepository.FindByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        }

        if (session.Status == SessionStatus.COMPLETED)
        {
            return Result.Failure<Guid>(Error.Validation("Session.Completed", "Buổi chơi đã kết thúc, không thể tuyển thêm khách giao lưu."));
        }

        // Hủy các bài đăng tìm khách đang active trước đó nếu có
        var activeRecruitments = await recruitmentRepository.FindAll(r => r.SessionId == request.SessionId && r.Status == RecruitmentStatus.ACTIVE)
            .ToListAsync(cancellationToken);

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

        recruitmentRepository.Add(recruitment);

        return Result.Success(recruitment.Id);
    }
}
