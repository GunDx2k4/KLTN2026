using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.GetMine;

public sealed record GetMyFeeQuery(Guid FeeId) : IQuery<PersonalFeeResponse>;
