using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Vote;

public sealed class VoteBookingCommandHandler(
    ISessionRepository sessionRepository,
    IBookingRepository bookingRepository,
    IUserRepository userRepository,
    ICurrentUserService currentUserService,
    ISessionRealtimePublisher? sessionRealtimePublisher = null,
    IClubMemberRepository? clubMemberRepository = null,
    INotificationRepository? notificationRepository = null,
    IDeviceRepository? deviceRepository = null,
    INotificationService? notificationService = null,
    ILogger<VoteBookingCommandHandler>? logger = null) : ICommandHandler<VoteBookingCommand, SessionBookingResponse>
{
    public async Task<Result<SessionBookingResponse>> Handle(VoteBookingCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<SessionBookingResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để đăng ký giữ chỗ."));
        }

        var session = await sessionRepository.GetAttendanceAsync(request.SessionId, true, cancellationToken);

        if (session is null)
        {
            return Result.Failure<SessionBookingResponse>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        }

        if (session.Status != SessionStatus.OPEN)
        {
            return Result.Failure<SessionBookingResponse>(Error.Validation("Session.NotOpen", "Buổi chơi hiện không mở đăng ký giữ chỗ."));
        }

        if (DateTime.UtcNow >= session.DeadlineBooking)
        {
            return Result.Failure<SessionBookingResponse>(Error.Validation("Session.DeadlinePassed", "Đã quá hạn chốt đăng ký giữ chỗ cho buổi chơi này."));
        }

        var user = await userRepository.FindByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return Result.Failure<SessionBookingResponse>(Error.NotFound("User.NotFound", "Không tìm thấy thông tin người dùng."));
        }

        var existingBooking = session.Bookings.FirstOrDefault(b => b.UserId == userId.Value);
        if (existingBooking != null && existingBooking.Status is BookingStatus.CONFIRMED or BookingStatus.WAITING or BookingStatus.CHECKED_IN or BookingStatus.NO_SHOW)
        {
            return Result.Failure<SessionBookingResponse>(Error.Conflict("Booking.AlreadyBooked", "Bạn đã thực hiện đăng ký giữ chỗ cho buổi chơi này rồi."));
        }

        var hasOverlap = await bookingRepository.HasOverlappingBookingAsync(
            userId.Value,
            session.StartTime,
            session.EndTime,
            session.Id,
            cancellationToken);

        if (hasOverlap)
        {
            return Result.Failure<SessionBookingResponse>(Error.Conflict("Booking.TimeOverlap", "Bạn đã có lịch tham gia buổi chơi khác trong cùng khung giờ này."));
        }

        var isClubMember = clubMemberRepository is null || await clubMemberRepository
            .FindAll(m => m.GroupId == session.GroupId && m.UserId == userId.Value && m.Status == ClubMemberStatus.ACTIVE && !m.IsDeleted)
            .AnyAsync(cancellationToken);

        var confirmedCount = session.Bookings.Count(RecruitmentRules.OccupiesSlot);
        var hasWaiters = session.Bookings.Any(b => !b.IsDeleted && b.Status == BookingStatus.WAITING);

        SessionBooking booking;
        if (isClubMember && confirmedCount < session.MaxPlayers && !hasWaiters)
        {
            // Slot chính thức (FR08)
            if (existingBooking != null)
            {
                existingBooking.UpdateStatus(BookingStatus.CONFIRMED);
                existingBooking.UpdateQueueOrder(0);
                existingBooking.AssignRecruitment(null);
                bookingRepository.Update(existingBooking);
                booking = existingBooking;
            }
            else
            {
                booking = new SessionBooking(session.Id, userId.Value, status: BookingStatus.CONFIRMED, queueOrder: 0);
                session.Bookings.Add(booking);
                bookingRepository.Add(booking);
            }
        }
        else
        {
            // Khách vãng lai HOẶC vượt trần -> Tự động nạp vào Hàng đợi (FR09, FR11)
            var maxQueueOrder = session.Bookings
                .Where(b => !b.IsDeleted && b.Status == BookingStatus.WAITING)
                .Select(b => (int?)b.QueueOrder)
                .Max() ?? 0;

            var nextQueueOrder = maxQueueOrder + 1;

            var activeRecruitment = !isClubMember
                ? session.Recruitments.FirstOrDefault(r => !r.IsDeleted && r.Status == RecruitmentStatus.ACTIVE)
                : null;

            if (existingBooking != null)
            {
                existingBooking.UpdateStatus(BookingStatus.WAITING);
                existingBooking.UpdateQueueOrder(nextQueueOrder);
                if (activeRecruitment != null)
                {
                    existingBooking.AssignRecruitment(activeRecruitment.Id);
                }
                bookingRepository.Update(existingBooking);
                booking = existingBooking;
            }
            else
            {
                booking = new SessionBooking(
                    sessionId: session.Id,
                    userId: userId.Value,
                    recruitmentId: activeRecruitment?.Id,
                    status: BookingStatus.WAITING,
                    queueOrder: nextQueueOrder
                );
                session.Bookings.Add(booking);
                bookingRepository.Add(booking);
            }
        }

        RecruitmentRules.Synchronize(session, DateTime.UtcNow);

        if (sessionRealtimePublisher != null)
        {
            await sessionRealtimePublisher.BookingUpdatedAsync(session.Id, userId.Value, booking.Status.ToString(), cancellationToken);
        }

        if (clubMemberRepository != null && notificationRepository != null && deviceRepository != null && notificationService != null)
        {
            try
            {
                var hostMembers = await clubMemberRepository
                    .FindAll(m => m.GroupId == session.GroupId && m.Status == ClubMemberStatus.ACTIVE && !m.IsDeleted && m.UserId != userId.Value)
                    .Include(m => m.RoleEntity)
                    .Where(m => m.RoleEntity.RoleName.ToLower().Contains("host") || m.RoleEntity.IsDefault == false)
                    .ToListAsync(cancellationToken);

                if (hostMembers.Count == 0)
                {
                    hostMembers = await clubMemberRepository
                        .FindAll(m => m.GroupId == session.GroupId && m.Status == ClubMemberStatus.ACTIVE && !m.IsDeleted && m.UserId != userId.Value)
                        .ToListAsync(cancellationToken);
                }

                if (hostMembers.Count > 0)
                {
                    var isWaiting = booking.Status == BookingStatus.WAITING;
                    var notifTitle = isWaiting
                        ? $"Có người đăng ký vào hàng đợi ({session.Title})"
                        : $"Có người chơi tham gia ({session.Title})";
                    var notifContent = isWaiting
                        ? $"{user.FullName} vừa đăng ký vào hàng đợi (#{booking.QueueOrder}) cho buổi chơi \"{session.Title}\". Vui lòng xét duyệt."
                        : $"{user.FullName} vừa đăng ký tham gia buổi chơi \"{session.Title}\".";

                    foreach (var host in hostMembers)
                    {
                        var notification = new Notification(
                            userId: host.UserId,
                            title: notifTitle,
                            content: notifContent,
                            type: NotificationType.SESSION,
                            referenceType: "Session",
                            referenceId: session.Id
                        );
                        notificationRepository.Add(notification);
                    }

                    var hostUserIds = hostMembers.Select(h => h.UserId).Distinct().ToList();
                    var tokens = await deviceRepository.GetActiveTokensByUserIdsAsync(hostUserIds, cancellationToken);
                    var validTokens = tokens.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.Ordinal).ToList();

                    if (validTokens.Count > 0)
                    {
                        var data = new Dictionary<string, string>
                        {
                            ["sessionId"] = session.Id.ToString(),
                            ["type"] = "SESSION"
                        };

                        await notificationService.SendMulticastNotificationAsync(
                            validTokens,
                            notifTitle,
                            notifContent,
                            data,
                            cancellationToken
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Gửi thông báo có người chơi mới thất bại: {Message}", ex.Message);
            }
        }

        return Result.Success(new SessionBookingResponse(
            booking.Id,
            session.Id,
            user.Id,
            user.FullName,
            user.AvatarUrl,
            user.PhoneNumber,
            user.SkillLevel.ToString(),
            booking.Status,
            booking.QueueOrder,
            booking.BookedAt,
            booking.CheckinAt,
            booking.RecruitmentId,
            user.Gender
        ));
    }
}
