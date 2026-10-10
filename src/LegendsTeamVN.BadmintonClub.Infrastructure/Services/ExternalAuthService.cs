using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Services;

public class ExternalAuthService(ILogger<ExternalAuthService> logger) : IExternalAuthService
{
    public async Task<ExternalAuthUserInfo?> VerifyTokenAsync(string idToken, string provider, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return null;

        var normalizedProvider = provider.Trim().ToLowerInvariant();

        try
        {
            // 1. Xác thực qua Firebase Admin SDK nếu FirebaseApp đã được khởi tạo
            if (FirebaseApp.DefaultInstance != null)
            {
                try
                {
                    var decodedToken = await FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken, cancellationToken);
                    var email = decodedToken.Claims.TryGetValue("email", out var emailClaim) ? emailClaim?.ToString() : null;
                    var name = decodedToken.Claims.TryGetValue("name", out var nameClaim) ? nameClaim?.ToString() : null;
                    var picture = decodedToken.Claims.TryGetValue("picture", out var pictureClaim) ? pictureClaim?.ToString() : null;

                    if (!string.IsNullOrEmpty(email))
                    {
                        return new ExternalAuthUserInfo(
                            Provider: string.IsNullOrEmpty(provider) ? "Firebase" : provider,
                            ProviderKey: decodedToken.Uid,
                            Email: email,
                            FullName: name ?? email.Split('@')[0],
                            AvatarUrl: picture
                        );
                    }
                }
                catch (FirebaseAuthException ex)
                {
                    logger.LogWarning(ex, "Firebase ID token verification failed. Falling back to provider-specific validation.");
                }
            }

            // 2. Xác thực Google OAuth ID Token trực tiếp
            if (normalizedProvider == "google")
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken);
                if (payload != null && !string.IsNullOrEmpty(payload.Email))
                {
                    return new ExternalAuthUserInfo(
                        Provider: "Google",
                        ProviderKey: payload.Subject,
                        Email: payload.Email,
                        FullName: payload.Name ?? payload.Email.Split('@')[0],
                        AvatarUrl: payload.Picture
                    );
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to verify external token for provider: {Provider}", provider);
            return null;
        }

        return null;
    }
}
