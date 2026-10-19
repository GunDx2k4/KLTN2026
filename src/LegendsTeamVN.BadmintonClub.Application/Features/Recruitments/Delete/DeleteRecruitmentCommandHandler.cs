using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Delete;

public sealed class DeleteRecruitmentCommandHandler(IRecruitmentRepository recruitmentRepository, ISessionRepository sessions) : ICommandHandler<DeleteRecruitmentCommand>
{
    public async Task<Result> Handle(DeleteRecruitmentCommand request, CancellationToken cancellationToken)
    {
        var session = await sessions.GetAttendanceAsync(request.SessionId, true, cancellationToken);
        if (session is null)
            return Result.Failure(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        var recruitments = session.Recruitments.Where(r => !r.IsDeleted &&
            r.Status is RecruitmentStatus.ACTIVE or RecruitmentStatus.FILLED).ToList();

        if (recruitments.Count == 0)
        {
            return Result.Failure(Error.NotFound("Recruitment.NotFound", "Không có tin tìm khách giao lưu nào đang mở cho buổi chơi này."));
        }

        foreach (var r in recruitments)
        {
            r.UpdateStatus(RecruitmentStatus.EXPIRED);
            recruitmentRepository.Update(r);
        }

        return Result.Success();
    }
}
