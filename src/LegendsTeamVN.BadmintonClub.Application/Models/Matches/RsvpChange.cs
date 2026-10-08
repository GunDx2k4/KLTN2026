using LegendsTeamVN.BadmintonClub.Application.DTOs.Matches.Responses;

namespace LegendsTeamVN.BadmintonClub.Application.Models.Matches;

public sealed record RsvpChange(MatchResponse Match, bool Changed);
