using LegendsTeamVN.Core.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class GroupBankAccount : Entity<Guid>
{
    public Guid GroupId { get; private set; }
    public string BankName { get; private set; } = default!;
    public string? BankBin { get; private set; }

    public static GroupBankAccount CreatePaymentAccount(Guid groupId, PaymentBankSnapshot bank)
    {
        if (!bank.IsValid || groupId == Guid.Empty) throw new ArgumentException("Thông tin ngân hàng không hợp lệ.");
        return new GroupBankAccount(groupId, bank.BankName, bank.AccountNumber, bank.AccountHolder) { BankBin = bank.BankBin };
    }

    public bool Matches(PaymentBankSnapshot bank) => IsActive && BankBin == bank.BankBin && BankName == bank.BankName &&
        AccountNumber == bank.AccountNumber && AccountHolder == bank.AccountHolder;
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
