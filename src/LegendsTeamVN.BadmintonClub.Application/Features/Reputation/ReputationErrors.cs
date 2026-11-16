using LegendsTeamVN.Core.Utilities.Results;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation;
public static class ReputationErrors
{
    public static readonly Error NotFound = Error.NotFound("Reputation.NotFound", "Không tìm thấy dữ liệu trong phạm vi yêu cầu.");
    public static readonly Error Forbidden = Error.Forbidden("Reputation.Forbidden", "Bạn không có quyền thực hiện thao tác này.");
    public static readonly Error Conflict = Error.Conflict("Reputation.Conflict", "Đánh giá chưa sẵn sàng, đã hết hạn hoặc khác nội dung đã gửi.");
    public static readonly Error Unauthorized = Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập.");
}
