using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger;

public interface ILedgerCommand : ICommand<LedgerWriteResponse>
{
    Guid GroupId { get; }
}
