using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Create;

public sealed record CreateClubCommand(
    string Name,
    string? Description = null,
    string? AvatarUrl = null,
    string? FixedVenueName = null,
    string? FixedAddress = null,
    decimal? Lat = null,
    decimal? Lng = null,
    SkillLevel MinSkillLevel = SkillLevel.NB,
    SkillLevel MaxSkillLevel = SkillLevel.K,
    List<CreateClubScheduleItemRequest>? Schedules = null
) : ICommand<Guid>;

