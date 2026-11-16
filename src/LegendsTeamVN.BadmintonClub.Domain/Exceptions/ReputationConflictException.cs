namespace LegendsTeamVN.BadmintonClub.Domain.Exceptions;
public sealed class ReputationConflictException(Exception? inner = null)
    : Exception("Dữ liệu uy tín đang được cập nhật. Vui lòng tải lại và thử lại.", inner);
