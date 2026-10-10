using LegendsTeamVN.BadmintonClub.Application.DTOs.Auth;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Auth.Register;

public sealed record RegisterCommand(
    string FullName,
    string? Email,
    string? PhoneNumber,
    string Password,
    short Gender = 0,
    SkillLevel SkillLevel = SkillLevel.NB
) : ICommand<RegisterResponse>;
