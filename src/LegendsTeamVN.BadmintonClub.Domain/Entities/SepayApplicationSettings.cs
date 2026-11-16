using LegendsTeamVN.Core.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class SepayApplicationSettings : Entity<int>
{
    public string ClientId { get; private set; } = "";
    public string ProtectedClientSecret { get; private set; } = "";
    public string PublicApiBaseUrl { get; private set; } = "";
    public string FrontendBaseUrl { get; private set; } = "";
    public bool Enabled { get; private set; }
    public Guid Revision { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    protected SepayApplicationSettings() { }

    public static SepayApplicationSettings Create() => new() { Id = 1 };

    public void Configure(string clientId, string protectedSecret, string apiUrl, string frontendUrl,
        bool enabled, Guid actor, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(clientId) || clientId.Length > 255 || string.IsNullOrWhiteSpace(protectedSecret) ||
            !IsPublicHttps(apiUrl) || !IsPublicHttps(frontendUrl) || actor == Guid.Empty || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Thông tin ứng dụng SePay không hợp lệ.");
        ClientId = clientId;
        ProtectedClientSecret = protectedSecret;
        PublicApiBaseUrl = apiUrl.TrimEnd('/');
        FrontendBaseUrl = frontendUrl.TrimEnd('/');
        Enabled = enabled;
        UpdatedBy = actor;
        UpdatedAt = now;
        Revision = Guid.NewGuid();
    }

    public static bool IsPublicHttps(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == "https" && !uri.IsLoopback && uri.Host.Contains('.') &&
        !System.Net.IPAddress.TryParse(uri.Host, out _) && uri.UserInfo.Length == 0 &&
        uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0 && value.Length <= 500;

    public string CallbackUrl => PublicApiBaseUrl + "/api/v1/sepay/oauth/callback";
    public string ClubReturnUrl(Guid groupId) => FrontendBaseUrl + "/#/club/" + groupId + "/sepay";
}
