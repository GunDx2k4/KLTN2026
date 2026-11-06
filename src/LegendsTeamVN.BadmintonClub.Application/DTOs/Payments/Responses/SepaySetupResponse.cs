namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;

public sealed record SepayApplicationResponse(string ClientId, bool HasClientSecret, string PublicApiBaseUrl,
    string FrontendBaseUrl, string CallbackUrl, bool Enabled, Guid? Revision, bool WebhookEnabled);
public sealed record SepayConnectionResponse(Guid? ConnectionId, Guid? BankAccountId, string Status, long? CompanyId,
    string? Gateway, DateTime? ConnectedAt, bool ApplicationReady, bool WebhookEnabled);
public sealed record SepayAuthorizationResponse(string AuthorizationUrl);
public sealed record SepayCallbackResponse(string ReturnUrl);
public sealed record SepayBankAccountResponse(long Id, string AccountNumber, string AccountHolder, string BankBin,
    string Gateway, bool Active, bool MatchesClubBank);
