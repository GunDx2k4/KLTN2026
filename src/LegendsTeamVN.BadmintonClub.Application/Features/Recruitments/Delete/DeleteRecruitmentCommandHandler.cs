using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Delete;

public sealed class DeleteRecruitmentCommandHandler(IRecruitmentRepository recruitmentRepository) : ICommandHandler<DeleteRecruitmentCommand>
{
    public async Task<Result> Handle(DeleteRecruitmentCommand request, CancellationToken cancellationToken)
    {
        var recruitments = await recruitmentRepository.FindAll(r => r.SessionId == request.SessionId && r.Status == RecruitmentStatus.ACTIVE)
            .ToListAsync(cancellationToken);

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
