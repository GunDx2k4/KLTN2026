using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.GetAttachment;

public sealed record GetLedgerAttachmentQuery(Guid ClubId, Guid LedgerId) : IQuery<LedgerAttachmentResponse>;
