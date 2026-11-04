using LegendsTeamVN.Core.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class ClubSepayConnection : Entity<Guid>
{
    public Guid GroupId { get; private set; }
    public Guid BankAccountId { get; private set; }
    public long CompanyId { get; private set; }
    public long? ProviderBankAccountId { get; private set; }
    public long? ProviderWebhookId { get; private set; }
    public string Gateway { get; private set; } = "";
    public string ProtectedTokens { get; private set; } = "";
    public string ProtectedWebhookSecret { get; private set; } = "";
    public Guid ApplicationRevision { get; private set; }
    public Guid AuthorizedBy { get; private set; }
    public DateTime AuthorizedAt { get; private set; }
    public DateTime? ConnectedAt { get; private set; }
    public DateTime? DisconnectedAt { get; private set; }
    public Guid? DisconnectedBy { get; private set; }
    public string Status { get; private set; } = "AUTHORIZED";
    public Guid Revision { get; private set; }
    protected ClubSepayConnection() { }

    public static ClubSepayConnection Authorize(GroupBankAccount bank, long companyId, string protectedTokens,
        Guid applicationRevision, Guid actor, DateTime now)
    {
        if (!bank.IsActive || string.IsNullOrWhiteSpace(bank.BankBin) || companyId <= 0 ||
            string.IsNullOrWhiteSpace(protectedTokens) || actor == Guid.Empty || applicationRevision == Guid.Empty || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Kết nối SePay không hợp lệ.");
        return new() { Id = Guid.NewGuid(), GroupId = bank.GroupId, BankAccountId = bank.Id,
            CompanyId = companyId, ProtectedTokens = protectedTokens, ApplicationRevision = applicationRevision,
            AuthorizedBy = actor, AuthorizedAt = now, Revision = Guid.NewGuid() };
    }

    public void SelectBank(GroupBankAccount bank, long providerId, string accountNumber, string bin,
        string gateway, bool active, string protectedWebhookSecret, DateTime now)
    {
        if (Status != "AUTHORIZED" || now > AuthorizedAt.AddMinutes(15) || BankAccountId != bank.Id || !bank.IsActive ||
            providerId <= 0 || !active || accountNumber != bank.AccountNumber || bin != bank.BankBin ||
            string.IsNullOrWhiteSpace(gateway) || gateway.Length > 100 || string.IsNullOrWhiteSpace(protectedWebhookSecret))
            throw new InvalidOperationException("Tài khoản SePay phải đang hoạt động và khớp ngân hàng nhận tiền của CLB; phiên kết nối có hạn 15 phút.");
        ProviderBankAccountId = providerId;
        Gateway = gateway;
        ProtectedWebhookSecret = protectedWebhookSecret;
        Status = "PROVISIONING";
        Revision = Guid.NewGuid();
    }

    public void Connect(long webhookId, DateTime now)
    {
        if (Status != "PROVISIONING" || webhookId <= 0 || now.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("Kết nối chưa sẵn sàng kích hoạt.");
        ProviderWebhookId = webhookId;
        ConnectedAt = now;
        Status = "CONNECTED";
        Revision = Guid.NewGuid();
    }

    public void RefreshTokens(string protectedTokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedTokens);
        if (Status == "DISCONNECTED") throw new InvalidOperationException("Kết nối đã ngắt.");
        ProtectedTokens = protectedTokens;
        Revision = Guid.NewGuid();
    }

    public void Disconnect(Guid actor, DateTime now)
    {
        if (actor == Guid.Empty || now.Kind != DateTimeKind.Utc) throw new ArgumentException("Thông tin ngắt kết nối không hợp lệ.");
        Status = "DISCONNECTED";
        ProtectedTokens = "";
        ProtectedWebhookSecret = "";
        DisconnectedAt = now;
        DisconnectedBy = actor;
        Revision = Guid.NewGuid();
    }

    public void BeginDisconnect(Guid actor, DateTime now)
    {
        if (actor == Guid.Empty || now.Kind != DateTimeKind.Utc || Status == "DISCONNECTED")
            throw new InvalidOperationException("Không thể ngắt kết nối này.");
        Status = "DISCONNECTING";
        DisconnectedBy = actor;
        Revision = Guid.NewGuid();
    }
}
