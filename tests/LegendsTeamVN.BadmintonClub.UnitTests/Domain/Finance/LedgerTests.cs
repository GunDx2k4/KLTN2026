using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Finance;

public sealed class LedgerTests
{
    [Fact]
    public void InsufficientBalanceOrInvalidAmountCannotCreateExpense()
    {
        var balance = new LedgerBalance(50000, 10000);
        Assert.Equal(40000m, balance.AvailableBalance);
        Assert.True(Expense(40000, balance).IsSuccess);
        Assert.Equal("Ledger.InsufficientBalance", Expense(40000.01m, balance).Error.Code);
        Assert.True(Expense(0, balance).IsFailure);
        Assert.True(Expense(1.001m, balance).IsFailure);
    }

    [Fact]
    public void AttachmentRejectsUnsafeNamesMimeMismatchAndOversizedDataAndCopiesBytes()
    {
        var ledger = Expense(100, new(100, 0)).Value;
        var bytes = "%PDF-1.7 data"u8.ToArray();
        Assert.True(LedgerAttachment.Create(ledger, "../a.pdf", "application/pdf", bytes).IsFailure);
        Assert.True(LedgerAttachment.Create(ledger, "a.png", "image/png", bytes).IsFailure);
        Assert.True(LedgerAttachment.Create(ledger, "a.pdf", "application/pdf", new byte[5242881]).IsFailure);
        var valid = LedgerAttachment.Create(ledger, "a.pdf", "application/pdf", bytes).Value;
        bytes[0] = 0;
        Assert.Equal((byte)'%', valid.Content[0]);
        Assert.Equal(64, valid.Sha256.Length);
    }

    private static LegendsTeamVN.Core.Utilities.Results.Result<GroupLedger> Expense(decimal amount, LedgerBalance balance) =>
        GroupLedger.CreateExpense(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), amount, LedgerCategory.OTHER,
            "Nước uống", balance, DateTime.UtcNow);
}
