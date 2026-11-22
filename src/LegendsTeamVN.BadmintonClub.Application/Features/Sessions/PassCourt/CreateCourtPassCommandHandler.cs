using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.PassCourt;

public sealed class CreateCourtPassCommandHandler(
    ISessionRepository sessionRepository,
    IRecruitmentRepository recruitmentRepository,
    IClubRepository clubRepository,
    IClubMemberRepository clubMemberRepository,
    ICurrentUserService currentUserService) : ICommandHandler<CreateCourtPassCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateCourtPassCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return Result.Failure<Guid>(Error.Unauthorized("Auth.Unauthorized", "Vui lòng đăng nhập để thực hiện tính năng này."));
        }

        var req = request.Request;

        if (string.IsNullOrWhiteSpace(req.CourtLocation))
        {
            return Result.Failure<Guid>(Error.Validation("CourtPass.LocationRequired", "Vui lòng nhập tên sân cầu lông."));
        }

        if (req.EndTime <= req.StartTime)
        {
            return Result.Failure<Guid>(Error.Validation("CourtPass.InvalidTime", "Giờ kết thúc phải sau giờ bắt đầu."));
        }

        if (req.StartTime.UtcDateTime <= DateTime.UtcNow)
        {
            return Result.Failure<Guid>(Error.Validation("CourtPass.PastTime", "Khung giờ pass sân phải ở tương lai."));
        }

        Guid groupId;
        if (req.ClubId.HasValue && req.ClubId.Value != Guid.Empty)
        {
            var club = await clubRepository.FindByIdAsync(req.ClubId.Value, cancellationToken);
            if (club is null)
            {
                return Result.Failure<Guid>(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ đã chọn."));
            }
            groupId = club.Id;
        }
        else
        {
            var member = await clubMemberRepository
                .FindAll(m => m.UserId == currentUserId.Value && m.Status == ClubMemberStatus.ACTIVE && !m.IsDeleted)
                .FirstOrDefaultAsync(cancellationToken);

            if (member != null)
            {
                groupId = member.GroupId;
            }
            else
            {
                var defaultClub = await clubRepository
                    .FindAll(c => c.IsActive && !c.IsDeleted)
                    .FirstOrDefaultAsync(cancellationToken);

                if (defaultClub is null)
                {
                    return Result.Failure<Guid>(Error.Failure("Club.NoneAvailable", "Chưa có câu lạc bộ khả dụng trong hệ thống."));
                }
                groupId = defaultClub.Id;
            }
        }

        var courtCount = req.CourtCount > 0 ? req.CourtCount : 1;
        var maxPlayers = courtCount * 4;
        var passPrice = req.PassPrice >= 0 ? req.PassPrice : 0;
        var deadline = req.StartTime.AddMinutes(-30);

        var title = $"[PASS SÂN] {req.CourtLocation}".Trim();
        if (title.Length > 255)
        {
            title = title[..255];
        }

        var rawLocation = req.CourtLocation.Trim();
        var courtLocation = rawLocation.StartsWith("[PASS SÂN]", StringComparison.OrdinalIgnoreCase)
            ? rawLocation
            : $"[PASS SÂN] {rawLocation}";
        if (!string.IsNullOrWhiteSpace(req.ContactPhone))
        {
            courtLocation += $" (LH: {req.ContactPhone.Trim()})";
        }
        if (courtLocation.Length > 500)
        {
            courtLocation = courtLocation[..500];
        }

        var address = req.Address?.Trim();
        if (!string.IsNullOrWhiteSpace(req.Note))
        {
            var noteTag = $"[Ghi chú]: {req.Note.Trim()}";
            address = string.IsNullOrWhiteSpace(address) ? noteTag : $"{address}\n{noteTag}";
        }
        if (address != null && address.Length > 500)
        {
            address = address[..500];
        }

        var session = new BadmintonSession(
            groupId: groupId,
            title: title,
            startTime: req.StartTime.UtcDateTime,
            endTime: req.EndTime.UtcDateTime,
            courtLocation: courtLocation,
            courtCount: courtCount,
            targetPlayerPerCourt: 4,
            maxPlayers: maxPlayers,
            deadlineBooking: deadline.UtcDateTime,
            courtRentalCost: passPrice,
            status: SessionStatus.OPEN,
            address: address,
            lat: req.Lat,
            lng: req.Lng
        );

        sessionRepository.Add(session);

        var recruitment = new GuestRecruitment(
            sessionId: session.Id,
            slotsNeeded: maxPlayers,
            minSkillLevel: req.MinSkillLevel ?? SkillLevel.NB,
            sharedCost: passPrice,
            scanRadiusKm: 15.0m,
            status: RecruitmentStatus.ACTIVE
        );

        session.Recruitments.Add(recruitment);
        recruitmentRepository.Add(recruitment);

        return Result.Success(session.Id);
    }
}
