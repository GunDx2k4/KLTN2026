using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Domain.Services;

public sealed record LedgerBalance
{
    public decimal TotalInflow { get; }
    public decimal TotalOutflow { get; }
    public decimal AvailableBalance => TotalInflow - TotalOutflow;

    public LedgerBalance(decimal totalInflow, decimal totalOutflow)
    {
        if (totalInflow < 0 || totalOutflow < 0)
            throw new ArgumentOutOfRangeException(nameof(totalInflow), "Tổng thu và tổng chi không được âm.");
        TotalInflow = totalInflow;
        TotalOutflow = totalOutflow;
    }

    public LedgerBalance WithReceipt(decimal amount) => new(checked(TotalInflow + amount), TotalOutflow);
    public LedgerBalance WithExpense(decimal amount) => new(TotalInflow, checked(TotalOutflow + amount));

    public Result CheckExpense(decimal amount)
    {
        if (!IsValidAmount(amount))
            return Result.Failure(Error.Validation("Ledger.InvalidAmount", "Số tiền phải dương, tối đa hai chữ số thập phân và trong giới hạn lưu trữ."));
        return amount <= AvailableBalance ? Result.Success() :
            Result.Failure(Error.Conflict("Ledger.InsufficientBalance", "Số dư quỹ không đủ"));
    }

    public static bool IsValidAmount(decimal amount) =>
        amount > 0 && amount <= 9999999999999999.99m && decimal.Round(amount, 2) == amount;
}
