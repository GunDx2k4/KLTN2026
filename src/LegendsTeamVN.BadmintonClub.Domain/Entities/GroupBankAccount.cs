using LegendsTeamVN.Core.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class GroupBankAccount : Entity<Guid>
{
    public Guid GroupId { get; private set; }
    public string BankName { get; private set; } = default!;
    public string AccountNumber { get; private set; } = default!;
    public string AccountHolder { get; private set; } = default!;
    public string? WebhookSecretKey { get; private set; }
    public bool IsActive { get; private set; }

    public virtual ClubGroup Group { get; private set; } = default!;

    protected GroupBankAccount() { }

    public GroupBankAccount(
        Guid groupId,
        string bankName,
        string accountNumber,
        string accountHolder,
        string? webhookSecretKey = null,
        bool isActive = true)
    {
        Id = Guid.NewGuid();
        GroupId = groupId;
        BankName = bankName;
        AccountNumber = accountNumber;
        AccountHolder = accountHolder;
        WebhookSecretKey = webhookSecretKey;
        IsActive = isActive;
    }

    public void UpdateInfo(string bankName, string accountNumber, string accountHolder, string? webhookSecretKey)
    {
        BankName = bankName;
        AccountNumber = accountNumber;
        AccountHolder = accountHolder;
        WebhookSecretKey = webhookSecretKey;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
