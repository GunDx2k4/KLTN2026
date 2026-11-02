namespace LegendsTeamVN.BadmintonClub.Domain.Exceptions;

public sealed class PaymentOperationConflictException(Exception innerException)
    : Exception("Dữ liệu thanh toán đang được cập nhật. Vui lòng tải lại và thử lại.", innerException);
