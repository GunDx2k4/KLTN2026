namespace LegendsTeamVN.BadmintonClub.Domain.Enums;

/// <summary>
/// Trạng thái đối soát: MATCHED (tự động khớp lệnh), MANUAL_CHECK (sai cú pháp/thiếu tiền).
/// </summary>
public enum ReconciliationStatus
{
    MATCHED = 1,
    MANUAL_CHECK = 2
}
