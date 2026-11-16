namespace LegendsTeamVN.BadmintonClub.Domain.Exceptions;

public sealed class ReconciliationWriteConflictException(Exception inner) : Exception("Nguồn thanh toán đã được xử lý đồng thời.", inner);
