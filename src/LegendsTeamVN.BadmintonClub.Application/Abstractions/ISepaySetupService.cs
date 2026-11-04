using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;

namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface ISepaySetupService
{
    Task<SepayApplicationResponse> GetApplicationAsync(Guid actor, CancellationToken ct);
    Task<SepayApplicationResponse> ConfigureApplicationAsync(Guid actor, ConfigureSepayApplicationRequest input, CancellationToken ct);
    Task<SepayConnectionResponse> GetConnectionAsync(Guid groupId, Guid actor, CancellationToken ct);
    Task<IReadOnlyList<SepayConnectionResponse>> ListConnectionsAsync(Guid groupId, Guid actor, CancellationToken ct);
    Task<SepayAuthorizationResponse> StartAsync(Guid groupId, Guid actor, CancellationToken ct);
    Task<SepayCallbackResponse> CallbackAsync(string state, string? code, string? error, CancellationToken ct);
    Task<IReadOnlyList<SepayBankAccountResponse>> AccountsAsync(Guid groupId, Guid actor, CancellationToken ct);
    Task<SepayConnectionResponse> SelectAsync(Guid groupId, Guid actor, long providerBankId, CancellationToken ct);
    Task<SepayConnectionResponse> ActivateAsync(Guid groupId, Guid actor, CancellationToken ct);
    Task<SepayConnectionResponse> DisconnectAsync(Guid groupId, Guid actor, Guid? connectionId, CancellationToken ct);
}

public sealed class SepaySetupException(string code, string message, int status = 409) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
