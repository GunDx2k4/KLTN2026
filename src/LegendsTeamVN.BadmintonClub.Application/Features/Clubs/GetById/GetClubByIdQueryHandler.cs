using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.GetById;

public sealed class GetClubByIdQueryHandler(
    IClubRepository clubRepository,
    ICurrentUserService currentUserService) : IQueryHandler<GetClubByIdQuery, ClubDetailResponse>
{
    public async Task<Result<ClubDetailResponse>> Handle(GetClubByIdQuery request, CancellationToken cancellationToken)
    {
        var club = await clubRepository.FindAll(c => c.Id == request.Id)
            .Include(c => c.Schedules)
            .Include(c => c.Members)
                .ThenInclude(m => m.User)
            .Include(c => c.Members)
                .ThenInclude(m => m.RoleEntity)
            .Include(c => c.JoinRequests)
            .FirstOrDefaultAsync(cancellationToken);

        if (club is null)
        {
            return Result.Failure<ClubDetailResponse>(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ."));
        }

        var currentUserId = currentUserService.UserId;
        var hostMember = club.Members.FirstOrDefault(m => m.RoleEntity != null && m.RoleEntity.IsHostRole && m.Status == ClubMemberStatus.ACTIVE);
        ClubHostResponse? hostResponse = null;
        if (hostMember?.User != null)
        {
            hostResponse = new ClubHostResponse(
                hostMember.User.Id,
                hostMember.User.FullName,
                hostMember.User.AvatarUrl,
                hostMember.User.PhoneNumber
            );
        }

        var schedulesResponse = club.Schedules
            .OrderBy(s => s.DayOfWeek)
            .Select(s => new ClubScheduleResponse(
                s.Id,
                s.DayOfWeek,
                GetDayName(s.DayOfWeek),
                s.StartTime.ToString("HH:mm"),
                s.EndTime.ToString("HH:mm"),
                s.IsActive
            ))
            .ToList();

        var isMember = false;
        string? roleName = null;
        var hasPendingJoinRequest = false;
        Guid? pendingRequestId = null;

        if (currentUserId.HasValue)
        {
            var myMembership = club.Members.FirstOrDefault(m => m.UserId == currentUserId.Value && m.Status == ClubMemberStatus.ACTIVE);
            if (myMembership != null)
            {
                isMember = true;
                roleName = myMembership.RoleEntity?.RoleName ?? "Thành viên";
            }
            else
            {
                var pendingRequest = club.JoinRequests.FirstOrDefault(jr => jr.UserId == currentUserId.Value && jr.Status == JoinRequestStatus.PENDING);
                if (pendingRequest != null)
                {
                    hasPendingJoinRequest = true;
                    pendingRequestId = pendingRequest.Id;
                }
            }
        }

        var activeMemberCount = club.Members.Count(m => m.Status == ClubMemberStatus.ACTIVE);

        var response = new ClubDetailResponse(
            club.Id,
            club.GroupName,
            club.Code,
            club.Description,
            club.AvatarUrl,
            club.FixedVenueName,
            club.FixedAddress,
            club.Lat,
            club.Lng,
            club.MinSkillLevel,
            club.MaxSkillLevel,
            club.GroupQualityScore,
            club.IsRecruiting,
            club.IsActive,
            activeMemberCount,
            hostResponse,
            schedulesResponse,
            new ClubUserMembershipStatus(isMember, roleName, hasPendingJoinRequest, pendingRequestId)
        );

        return Result.Success(response);
    }

    private static string GetDayName(int dayOfWeek) => dayOfWeek switch
    {
        0 => "Chủ nhật",
        1 => "Thứ hai",
        2 => "Thứ ba",
        3 => "Thứ tư",
        4 => "Thứ năm",
        5 => "Thứ sáu",
        6 => "Thứ bảy",
        _ => $"Thứ {dayOfWeek + 1}"
    };
}
