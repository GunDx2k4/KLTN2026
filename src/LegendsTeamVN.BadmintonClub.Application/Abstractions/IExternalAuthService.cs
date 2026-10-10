namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public record ExternalAuthUserInfo(
    string Provider,
    string ProviderKey,
    string Email,
    string FullName,
    string? AvatarUrl
);

public interface IExternalAuthService
{
    Task<ExternalAuthUserInfo?> VerifyTokenAsync(string idToken, string provider, CancellationToken cancellationToken = default);
}
