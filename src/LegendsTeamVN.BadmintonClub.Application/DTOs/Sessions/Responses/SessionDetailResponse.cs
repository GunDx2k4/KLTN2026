using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;

public sealed record SessionDetailResponse(
    SessionResponse Session,
    List<SessionBookingResponse> ConfirmedPlayers,
    List<SessionBookingResponse> WaitingQueue,
    GuestRecruitmentResponse? ActiveRecruitment
);
