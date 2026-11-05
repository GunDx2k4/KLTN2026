namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;

public sealed record ConfigureSepayApplicationRequest(string ClientId, string? ClientSecret,
    string PublicApiBaseUrl, string FrontendBaseUrl, bool Enabled, Guid? Revision)
{
    public override string ToString() => nameof(ConfigureSepayApplicationRequest);
}
