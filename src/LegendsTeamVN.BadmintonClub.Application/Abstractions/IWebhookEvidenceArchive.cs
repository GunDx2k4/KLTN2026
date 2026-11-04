namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface IWebhookEvidenceArchive
{
    Task ArchiveAsync(Guid bankAccountId, long transactionId, byte[] body, CancellationToken ct);
}
