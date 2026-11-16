using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Payments;

public sealed class SepayWebhookVerifier(ISepayWebhookCredentials credentials, TimeProvider clock) : ISepayWebhookVerifier
{
    public async Task<Result<VerifiedSepayWebhook>> VerifyAsync(Guid bankAccountId, byte[] body, string signature, string timestamp, CancellationToken ct)
    {
        var credential = await credentials.GetAsync(bankAccountId, ct);
        if (credential is null || string.IsNullOrWhiteSpace(credential.CompanyId) ||
            string.IsNullOrWhiteSpace(credential.Secret) || string.IsNullOrWhiteSpace(credential.Gateway))
            return Result.Failure<VerifiedSepayWebhook>(Error.Failure("Reconciliation.CredentialUnavailable", "Chưa có cấu hình xác thực webhook."));
        if (body is not { Length: > 0 and <= 65536 } || signature is null || !signature.StartsWith("sha256=", StringComparison.Ordinal) ||
            signature.Length != 71 || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            seconds < 0 || seconds < clock.GetUtcNow().ToUnixTimeSeconds() - 300 || seconds > clock.GetUtcNow().ToUnixTimeSeconds() + 300)
            return Unauthorized();
        byte[] supplied;
        try { supplied = Convert.FromHexString(signature[7..]); }
        catch (FormatException) { return Unauthorized(); }
        var prefix = Encoding.ASCII.GetBytes(timestamp + ".");
        var signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0); body.CopyTo(signed, prefix.Length);
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(credential.Secret), signed);
        if (!CryptographicOperations.FixedTimeEquals(expected, supplied)) return Unauthorized();
        try
        {
            using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                document.RootElement.EnumerateObject().GroupBy(p => p.Name, StringComparer.Ordinal).Any(g => g.Count() != 1))
                return Invalid();
            var payload = document.Deserialize<SepayWebhookRequest>();
            if (payload is null || payload.Id <= 0 || string.IsNullOrWhiteSpace(payload.Gateway) || payload.Gateway.Length > 100 ||
                string.IsNullOrWhiteSpace(payload.AccountNumber) || payload.AccountNumber.Length > 100 ||
                payload.SubAccount is { Length: > 100 } || payload.ReferenceCode is { Length: > 100 } || payload.Content is null ||
                payload.TransferType is not ("in" or "out") || payload.TransferAmount <= 0 ||
                payload.TransferAmount != decimal.Truncate(payload.TransferAmount) || payload.TransferAmount > 9999999999999999m ||
                !DateTime.TryParseExact(payload.TransactionDate, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var local)) return Invalid();
            var utc = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(7)).UtcDateTime;
            return Result.Success(new VerifiedSepayWebhook(new PaymentReconciliationEvidence(payload.Id, payload.Gateway,
                payload.AccountNumber, payload.SubAccount, payload.TransferType, payload.TransferAmount, utc,
                payload.ReferenceCode, payload.Content), credential.Gateway, credential.CompanyId, credential.ConnectionId));
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or OverflowException) { return Invalid(); }
    }

    private static Result<VerifiedSepayWebhook> Unauthorized() => Result.Failure<VerifiedSepayWebhook>(
        Error.Unauthorized("Reconciliation.InvalidSignature", "Chữ ký webhook không hợp lệ hoặc đã hết hạn."));
    private static Result<VerifiedSepayWebhook> Invalid() => Result.Failure<VerifiedSepayWebhook>(
        Error.Validation("Reconciliation.InvalidPayload", "Dữ liệu giao dịch không hợp lệ."));
}
