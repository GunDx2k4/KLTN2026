using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.GetMine;

public sealed record GetMyFeeQrImageQuery(Guid FeeId) : IQuery<byte[]>;
