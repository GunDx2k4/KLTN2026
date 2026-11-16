using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.CreateExpense;

public sealed class CreateLedgerExpenseCommandHandler(ILedgerWriteRepository repository, LedgerWriteSupport support, TimeProvider clock)
    : ICommandHandler<CreateLedgerExpenseCommand, LedgerWriteResponse>
{
    public async Task<Result<LedgerWriteResponse>> Handle(CreateLedgerExpenseCommand request, CancellationToken ct)
    {
        var access = await support.BeginAsync(request.GroupId, ct);
        if (access.IsFailure) return Result.Failure<LedgerWriteResponse>(access.Error);
        var old = await repository.FindRequestAsync(request.GroupId, request.RequestId, ct);
        if (old is not null)
        {
            var attachment = await repository.FindAttachmentAsync(old.Id, ct);
            if (!old.MatchesExpenseRequest(access.Value, request.Amount, request.Category, request.Description, request.SessionId,
                attachment, request.FileName, request.ContentType, request.Content)) return LedgerWriteSupport.Conflict();
            var current = await repository.GetBalanceAsync(request.GroupId, ct);
            return Result.Success(new LedgerWriteResponse(old.Id, null, old.Amount, current.AvailableBalance, null, "RECORDED", true, clock.GetUtcNow().UtcDateTime));
        }
        if (request.SessionId is Guid session && !await repository.SessionBelongsToGroupAsync(request.GroupId, session, ct))
            return Result.Failure<LedgerWriteResponse>(Error.NotFound("Ledger.SessionNotFound", "Buổi chơi không thuộc nhóm."));
        var balance = await repository.GetBalanceAsync(request.GroupId, ct);
        var created = GroupLedger.CreateExpense(request.GroupId, access.Value, request.RequestId, request.Amount,
            request.Category, request.Description, balance, clock.GetUtcNow().UtcDateTime, request.SessionId);
        if (created.IsFailure) return Result.Failure<LedgerWriteResponse>(created.Error);
        var ledger = created.Value;
        if (request.Content is not null)
        {
            var file = LedgerAttachment.Create(ledger, request.FileName!, request.ContentType!, request.Content);
            if (file.IsFailure) return Result.Failure<LedgerWriteResponse>(file.Error);
            repository.Add(file.Value);
        }
        repository.Add(ledger);
        return Result.Success(new LedgerWriteResponse(ledger.Id, null, ledger.Amount, balance.WithExpense(ledger.Amount).AvailableBalance, null, "RECORDED", false, clock.GetUtcNow().UtcDateTime));
    }
}
