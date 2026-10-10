using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Domain.Constants;

public static class GroupPermissions
{
    public static class Operations
    {
        public const string ScheduleManage = "OPERATIONS.SCHEDULE.MANAGE";
        public const string SessionView = "OPERATIONS.SESSION.VIEW";
        public const string SessionCreate = "OPERATIONS.SESSION.CREATE";
        public const string SessionUpdate = "OPERATIONS.SESSION.UPDATE";
        public const string SessionDelete = "OPERATIONS.SESSION.DELETE";
        public const string BookingManage = "OPERATIONS.BOOKING.MANAGE";
        public const string MatchManage = "OPERATIONS.MATCH.MANAGE";
        public const string RecruitManage = "OPERATIONS.RECRUIT.MANAGE";
    }

    public static class Finance
    {
        public const string LedgerView = "FINANCE.LEDGER.VIEW";
        public const string LedgerManage = "FINANCE.LEDGER.MANAGE";
        public const string FeeView = "FINANCE.FEE.VIEW";
        public const string FeeManage = "FINANCE.FEE.MANAGE";
        public const string BankManage = "FINANCE.BANK.MANAGE";
    }

    public static class Member
    {
        public const string View = "MEMBER.VIEW";
        public const string JoinRequestManage = "MEMBER.JOIN_REQUEST.MANAGE";
        public const string RoleManage = "MEMBER.ROLE.MANAGE";
        public const string Remove = "MEMBER.REMOVE";
        public const string InfoUpdate = "MEMBER.INFO.UPDATE";
    }

    public record PermissionDefinition(string Code, string Name, ModuleGroup ModuleGroup, string Description);

    public static readonly IReadOnlyList<PermissionDefinition> All = new List<PermissionDefinition>
    {
        // Operations (8 permissions)
        new(Operations.ScheduleManage, "Quản lý lịch sinh hoạt cố định", ModuleGroup.OPERATIONS, "Tạo, sửa và xóa lịch sinh hoạt lặp lại hàng tuần của nhóm."),
        new(Operations.SessionView, "Xem buổi sinh hoạt", ModuleGroup.OPERATIONS, "Xem danh sách và chi tiết các buổi sinh hoạt cầu lông."),
        new(Operations.SessionCreate, "Tạo buổi sinh hoạt", ModuleGroup.OPERATIONS, "Lên lịch và mở buổi sinh hoạt cầu lông mới."),
        new(Operations.SessionUpdate, "Cập nhật buổi sinh hoạt", ModuleGroup.OPERATIONS, "Chỉnh sửa thông tin, thời gian, sân bãi của buổi chơi."),
        new(Operations.SessionDelete, "Hủy buổi sinh hoạt", ModuleGroup.OPERATIONS, "Hủy hoặc xóa buổi sinh hoạt cầu lông."),
        new(Operations.BookingManage, "Quản lý đặt chỗ & điểm danh", ModuleGroup.OPERATIONS, "Duyệt đặt chỗ, điểm danh và xếp slot người chơi."),
        new(Operations.MatchManage, "Quản lý trận đấu & ghép sân", ModuleGroup.OPERATIONS, "Tạo trận đấu, ghép cặp và cập nhật kết quả trận đấu."),
        new(Operations.RecruitManage, "Tuyển khách vãng lai", ModuleGroup.OPERATIONS, "Đăng tin tuyển khách và quản lý lượt đăng ký vãng lai."),

        // Finance (5 permissions)
        new(Finance.LedgerView, "Xem sổ quỹ thu chi", ModuleGroup.FINANCE, "Xem báo cáo thu chi và số dư quỹ câu lạc bộ."),
        new(Finance.LedgerManage, "Quản lý phiếu thu chi quỹ", ModuleGroup.FINANCE, "Lập, sửa và hủy phiếu thu chi quỹ nhóm phát sinh."),
        new(Finance.FeeView, "Xem danh sách hội phí", ModuleGroup.FINANCE, "Xem danh sách và trạng thái nộp phí của người chơi."),
        new(Finance.FeeManage, "Quản lý thu phí & đối soát", ModuleGroup.FINANCE, "Xác nhận nộp phí, miễn giảm hoặc kiểm tra đối soát ngân hàng."),
        new(Finance.BankManage, "Quản lý tài khoản ngân hàng & quy tắc phí", ModuleGroup.FINANCE, "Cấu hình tài khoản ngân hàng nhận tiền và các quy tắc tính phí nhóm."),

        // Member (5 permissions)
        new(Member.View, "Xem danh sách thành viên", ModuleGroup.MEMBER, "Xem danh sách thành viên và khách trong câu lạc bộ."),
        new(Member.JoinRequestManage, "Duyệt yêu cầu gia nhập nhóm", ModuleGroup.MEMBER, "Duyệt hoặc từ chối đơn xin gia nhập nhóm của người chơi."),
        new(Member.RoleManage, "Quản lý vai trò & phân quyền", ModuleGroup.MEMBER, "Thay đổi vai trò hoặc cấu hình quyền hạn cho thành viên."),
        new(Member.Remove, "Xóa thành viên khỏi nhóm", ModuleGroup.MEMBER, "Xóa hoặc cấm thành viên rời khỏi câu lạc bộ."),
        new(Member.InfoUpdate, "Cập nhật thông tin câu lạc bộ", ModuleGroup.MEMBER, "Thay đổi tên nhóm, ảnh đại diện, địa chỉ sân cố định.")
    };

    public static int TotalCount => All.Count;
}
