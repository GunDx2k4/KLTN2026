using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Resume;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Pause;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Get;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Attendance.AddWalkIn;
using LegendsTeamVN.BadmintonClub.Application.Features.Attendance.Complete;
using LegendsTeamVN.BadmintonClub.Application.Features.Attendance.GetList;
using LegendsTeamVN.BadmintonClub.Application.Features.Attendance.Update;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Get;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Initialize;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Generate;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Start;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Finish;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ManualLineup;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.RestoreLineup;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Pause;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ConfirmManual;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Cancel;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.GetList;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.GetMyBooking;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.GetMyBookings;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Vote;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Confirm;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Cancel;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Create;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Delete;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Explore;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.GetBySession;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Join;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Create;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Delete;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.GetById;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.GetListByClub;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Update;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.UpdateStatus;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Presentation.Security;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints;

public class SessionEndpoint : EndpointGroupBase
{
    protected override string Name => "sessions";
    protected override string? Tag => null;

    protected override void Map(RouteGroupBuilder group)
    {
        var fr13Group = group.MapGroup("").WithTags("FR13 - Khởi tạo buổi chơi");
        var fr08fr10Group = group.MapGroup("").WithTags("FR08-FR10 - Đăng ký giữ chỗ & Hàng đợi");
        var fr11Group = group.MapGroup("").WithTags("FR11 - Phát tín hiệu tìm khách giao lưu");
        var fr12Group = group.MapGroup("").WithTags("FR12 - Khách vãng lai đăng ký tham gia");
        var fr14Group = group.MapGroup("").WithTags("FR14 - Điểm danh tại sân");
        var fr15Group = group.MapGroup("").WithTags("FR15 - Điều phối xoay tua sân");
        var fr16Group = group.MapGroup("").WithTags("FR16 - Can thiệp đội hình thủ công");
        var fr17Group = group.MapGroup("").WithTags("FR17 - Lịch thi đấu cá nhân");

        // ==========================================
        // --- FR13: KHỞI TẠO & CẤU HÌNH BUỔI CHƠI ---
        // ==========================================
        fr13Group.MapPost("club/{clubId:guid}", CreateSession)
            .WithName("CreateSession")
            .WithSummary("Khởi tạo buổi chơi mới cho câu lạc bộ (FR13)")
            .RequireGroupPermission(GroupPermissions.Operations.SessionCreate);

        fr13Group.MapGet("club/{clubId:guid}", GetSessionsByClub)
            .WithName("GetSessionsByClub")
            .WithSummary("Lấy danh sách các buổi chơi của câu lạc bộ (FR13)")
            .RequireAuthorization();

        fr13Group.MapGet("{sessionId:guid}", GetSessionById)
            .WithName("GetSessionById")
            .WithSummary("Xem thông tin chi tiết buổi chơi, sĩ số và người tham gia (FR13)")
            .RequireAuthorization();

        fr13Group.MapPut("{sessionId:guid}", UpdateSession)
            .WithName("UpdateSession")
            .WithSummary("Cập nhật thông tin vận hành buổi chơi (FR13)")
            .RequireGroupPermission(GroupPermissions.Operations.SessionUpdate);

        fr13Group.MapPatch("{sessionId:guid}/status", UpdateSessionStatus)
            .WithName("UpdateSessionStatus")
            .WithSummary("Cập nhật trạng thái buổi chơi (Khóa sổ, Đang chơi, Hoàn thành) (FR13)")
            .RequireGroupPermission(GroupPermissions.Operations.SessionUpdate);

        fr13Group.MapDelete("{sessionId:guid}", DeleteSession)
            .WithName("DeleteSession")
            .WithSummary("Hủy buổi chơi cầu lông (FR13)")
            .RequireGroupPermission(GroupPermissions.Operations.SessionDelete);

        // ==============================================================
        // --- FR08, FR09, FR10: ĐĂNG KÝ GIỮ CHỖ & HÀNG ĐỢI TỰ ĐỘNG ---
        // ==============================================================
        fr08fr10Group.MapPost("{sessionId:guid}/bookings", VoteBooking)
            .WithName("VoteBooking")
            .WithSummary("Đăng ký giữ chỗ tham gia buổi chơi (Tự động vào hàng đợi nếu đầy) (FR08, FR09)")
            .RequireAuthorization();

        fr08fr10Group.MapDelete("{sessionId:guid}/bookings/me", CancelBooking)
            .WithName("CancelBooking")
            .WithSummary("Hủy giữ chỗ / Báo vắng (Tự động đôn người chờ đầu hàng đợi lên chính thức) (FR10)")
            .RequireAuthorization();

        fr08fr10Group.MapGet("my-bookings", GetMyBookings)
            .WithName("GetMyBookings")
            .WithSummary("Xem danh sách tất cả các lịch/buổi chơi mình đã đăng ký giữ chỗ (FR08, FR10)")
            .RequireAuthorization();

        fr08fr10Group.MapGet("{sessionId:guid}/bookings", GetSessionBookings)
            .WithName("GetSessionBookings")
            .WithSummary("Xem danh sách thành viên đã đăng ký giữ chỗ và hàng đợi chờ (FR08, FR09)")
            .RequireAuthorization();

        fr08fr10Group.MapGet("{sessionId:guid}/bookings/me", GetMyBooking)
            .WithName("GetMyBooking")
            .WithSummary("Kiểm tra trạng thái đăng ký giữ chỗ của cá nhân (FR08)")
            .RequireAuthorization();

        fr08fr10Group.MapPost("{sessionId:guid}/bookings/{bookingId:guid}/confirm", ConfirmBooking)
            .WithName("ConfirmBooking")
            .WithSummary("Host duyệt người chơi từ hàng đợi vào danh sách chính thức (FR09)")
            .RequireGroupPermission(GroupPermissions.Operations.BookingManage);

        // =======================================================
        // --- FR11: HOST PHÁT TÍN HIỆU TÌM KHÁCH GIAO LƯU LÂN CẬN ---
        // =======================================================
        fr11Group.MapPost("{sessionId:guid}/recruitments", CreateRecruitment)
            .WithName("CreateRecruitment")
            .WithSummary("Host phát tín hiệu tìm người giao lưu lân cận khi thiếu người (FR11)")
            .RequireGroupPermission(GroupPermissions.Operations.RecruitManage);

        fr11Group.MapGet("{sessionId:guid}/recruitments", GetSessionRecruitment)
            .WithName("GetSessionRecruitment")
            .WithSummary("Xem thông tin bài đăng tìm khách giao lưu của buổi chơi (FR11)")
            .RequireAuthorization();

        fr11Group.MapDelete("{sessionId:guid}/recruitments", DeleteRecruitment)
            .WithName("DeleteRecruitment")
            .WithSummary("Hủy / Đóng bài đăng tìm khách giao lưu (FR11)")
            .RequireGroupPermission(GroupPermissions.Operations.RecruitManage);

        // ==============================================================
        // --- FR12: TIẾP NHẬN & ĐĂNG KÝ SLOT GIAO LƯU CHO KHÁCH VÃNG LAI ---
        // ==============================================================
        fr12Group.MapGet("recruitments/explore", ExploreRecruitments)
            .WithName("ExploreRecruitments")
            .WithSummary("Khách vãng lai quét tìm các buổi giao lưu quanh tọa độ GPS (FR12)")
            .RequireAuthorization();

        fr12Group.MapPost("recruitments/{recruitmentId:guid}/join", JoinRecruitment)
            .WithName("JoinRecruitment")
            .WithSummary("Khách vãng lai xác nhận tham gia trực tiếp vào suất chơi giao lưu còn trống (FR12)")
            .RequireAuthorization();

        fr12Group.MapDelete("recruitments/{recruitmentId:guid}/cancel", CancelRecruitmentBooking)
            .WithName("CancelRecruitmentBooking")
            .WithSummary("Khách vãng lai hủy suất giao lưu đã đăng ký (FR12)")
            .RequireAuthorization();

        // ======================================
        // --- FR14: ĐIỂM DANH THÀNH VIÊN TẠI SÂN ---
        // ======================================
        // Quyền tại nhóm sở hữu session được kiểm tra trong AttendanceService.
        fr14Group.MapGet("{sessionId:guid}/attendance", GetSessionAttendance)
            .WithName("GetSessionAttendance")
            .WithSummary("Xem danh sách điểm danh và sĩ số thực tế tại sân (FR14)")
            .Produces<SessionAttendanceResponse>()
            .RequireGroupPermission(GroupPermissions.Operations.BookingManage);

        fr14Group.MapPut("{sessionId:guid}/attendance/{userId:guid}", UpdateSessionAttendance)
            .WithName("UpdateSessionAttendance")
            .WithSummary("Đánh dấu có mặt hoặc không đến sân cho người chơi (FR14)")
            .Produces<SessionAttendanceResponse>()
            .RequireGroupPermission(GroupPermissions.Operations.BookingManage);

        fr14Group.MapPost("{sessionId:guid}/attendance/walk-ins", AddSessionWalkIn)
            .WithName("AddSessionWalkIn")
            .WithSummary("Thêm nhanh người chơi có tài khoản và xác nhận có mặt (FR14)")
            .Produces<SessionAttendanceResponse>()
            .RequireGroupPermission(GroupPermissions.Operations.BookingManage);

        fr14Group.MapPost("{sessionId:guid}/attendance/complete", CompleteSessionAttendance)
            .WithName("CompleteSessionAttendance")
            .WithSummary("Hoàn tất điểm danh, cung cấp danh sách sẵn sàng cho điều phối (FR14)")
            .Produces<SessionAttendanceResponse>()
            .RequireGroupPermission(GroupPermissions.Operations.BookingManage);

        // ======================================
        // --- FR15: ĐIỀU PHỐI XOAY TUA SÂN ---
        // ======================================
        fr15Group.MapGet("{sessionId:guid}/rotation", GetRotation)
            .WithName("GetRotation")
            .WithSummary("Xem trạng thái sân và đề xuất điều phối (FR15)")
            .Produces<RotationResponse>()
            .RequireAuthorization("SessionOperations")
            .RequireGroupPermission(GroupPermissions.Operations.MatchManage);

        fr15Group.MapPost("{sessionId:guid}/rotation/initialize", InitializeRotation)
            .WithName("InitializeRotation")
            .WithSummary("Bắt đầu điều phối sau điểm danh (FR15)")
            .Produces<RotationResponse>()
            .RequireAuthorization("SessionOperations")
            .RequireGroupPermission(GroupPermissions.Operations.MatchManage);

        fr15Group.MapPost("{sessionId:guid}/rotation/proposals", GenerateRotationProposals)
            .WithName("GenerateRotationProposals")
            .WithSummary("Bổ sung đề xuất cho sân trống (FR15)")
            .Produces<RotationResponse>()
            .RequireAuthorization("SessionOperations")
            .RequireGroupPermission(GroupPermissions.Operations.MatchManage);

        fr15Group.MapPost("{sessionId:guid}/matches/{matchId:guid}/start", StartRotationMatch)
            .WithName("StartRotationMatch")
            .WithSummary("Xác nhận bắt đầu trận đã đề xuất (FR15)")
            .Produces<RotationResponse>()
            .RequireAuthorization("SessionOperations")
            .RequireGroupPermission(GroupPermissions.Operations.MatchManage);

        fr15Group.MapPost("{sessionId:guid}/matches/{matchId:guid}/finish", FinishRotationMatch)
            .WithName("FinishRotationMatch")
            .WithSummary("Kết thúc trận và đề xuất lượt tiếp theo (FR15)")
            .Produces<RotationResponse>()
            .RequireAuthorization("SessionOperations")
            .RequireGroupPermission(GroupPermissions.Operations.MatchManage);

        // --- FR16: CAN THIỆP ĐỘI HÌNH THỦ CÔNG ---
        fr16Group.MapPost("{sessionId:guid}/matches/{matchId:guid}/confirm-manual-start", ConfirmManualMatch)
            .WithName("ConfirmManualMatch")
            .WithSummary("Host xác nhận đội hình và bắt đầu trận kèm version (FR16)")
            .Produces<RotationResponse>()
            .RequireAuthorization("SessionOperations")
            .RequireGroupPermission(GroupPermissions.Operations.MatchManage);

        fr16Group.MapPut("{sessionId:guid}/rotation/players/{userId:guid}/pause", SetRotationPause)
            .WithName("SetRotationPause")
            .WithSummary("Host đặt tạm nghỉ hoặc sẵn sàng trở lại (FR16)")
            .Produces<RotationResponse>()
            .RequireAuthorization("SessionOperations")
            .RequireGroupPermission(GroupPermissions.Operations.MatchManage);

        fr16Group.MapPost("{sessionId:guid}/matches/{matchId:guid}/restore-proposal", RestoreManualLineup)
            .WithName("RestoreManualLineup")
            .WithSummary("Host khôi phục gợi ý đội hình ban đầu (FR16)")
            .Produces<RotationResponse>()
            .RequireAuthorization("SessionOperations")
            .RequireGroupPermission(GroupPermissions.Operations.MatchManage);

        fr16Group.MapPut("{sessionId:guid}/matches/{matchId:guid}/manual-lineup", UpdateManualLineup)
            .WithName("UpdateManualLineup")
            .WithSummary("Host điều chỉnh đội hình trước khi bắt đầu (FR16)")
            .Produces<RotationResponse>()
            .RequireAuthorization("SessionOperations")
            .RequireGroupPermission(GroupPermissions.Operations.MatchManage);

        // --- FR17: LỊCH THI ĐẤU CÁ NHÂN ---
        fr17Group.MapGet("{sessionId:guid}/my-matches", GetMyMatches)
            .WithName("GetMyMatches")
            .Produces<MyMatchesResponse>()
            .RequireAuthorization("SessionOperations");

        fr17Group.MapPost("{sessionId:guid}/my-matches/pause", PauseMyMatches)
            .WithName("PauseMyMatches")
            .Produces<MyMatchesResponse>()
            .RequireAuthorization("SessionOperations");

        fr17Group.MapPost("{sessionId:guid}/my-matches/resume", ResumeMyMatches)
            .WithName("ResumeMyMatches")
            .Produces<MyMatchesResponse>()
            .RequireAuthorization("SessionOperations");

        // ======================================
        // FR18 - FR20: Hạch toán & Quyết toán chi phí đa hệ số (Settlement)
        // ======================================
        var fr18Group = group.MapGroup("")
            .WithTags("Session Operations - FR18-FR20 Settlement")
            .RequireAuthorization();


        fr18Group.MapGet("{sessionId:guid}/settlement/form", GetSettlementForm)
            .WithName("GetSettlementForm")
            .Produces<SettlementFormResponse>()
            .RequireGroupPermission(GroupPermissions.Finance.FeeManage);

        fr18Group.MapPost("{sessionId:guid}/settlement/preview", PreviewSettlement)
            .WithName("PreviewSettlement")
            .Produces<SettlementPreviewResponse>()
            .RequireGroupPermission(GroupPermissions.Finance.FeeManage);

        fr18Group.MapPost("{sessionId:guid}/settlement/approve", ApproveSettlement)
            .WithName("ApproveSettlement")
            .Produces<SessionSettlementResponse>()
            .RequireGroupPermission(GroupPermissions.Finance.FeeManage);

        fr18Group.MapGet("{sessionId:guid}/settlement", GetSessionSettlement)
            .WithName("GetSessionSettlement")
            .WithSummary("Xem thông tin quyết toán chi phí buổi chơi (FR18-FR20)")
            .Produces<SessionSettlementResponse>()
            .RequireGroupPermission(GroupPermissions.Finance.FeeView);

        fr18Group.MapPost("{sessionId:guid}/settlement/members/{userId:guid}/pay", MarkMemberSettlementPaid)
            .WithName("MarkMemberSettlementPaid")
            .WithSummary("Đánh dấu thành viên đã thanh toán tiền mặt (FR18-FR20)")
            .Produces<SessionSettlementResponse>()
            .RequireGroupPermission(GroupPermissions.Finance.FeeManage);
    }

    // --- FR13 Handlers ---
    private static async Task<IResult> CreateSession(
        [FromRoute] Guid clubId,
        [FromBody] CreateSessionRequest request,
        ISender sender)
    {
        var command = new CreateSessionCommand(clubId, request);
        var result = await sender.Send(command);
        return result.Match(id => Results.Created($"/api/v1/sessions/{id}", new { SessionId = id }));
    }

    private static async Task<IResult> GetSessionsByClub(
        [FromRoute] Guid clubId,
        ISender sender)
    {
        var query = new GetSessionsByClubQuery(clubId);
        var result = await sender.Send(query);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> GetSessionById(
        [FromRoute] Guid sessionId,
        ISender sender)
    {
        var query = new GetSessionByIdQuery(sessionId);
        var result = await sender.Send(query);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> UpdateSession(
        [FromRoute] Guid sessionId,
        [FromBody] UpdateSessionRequest request,
        ISender sender)
    {
        var command = new UpdateSessionCommand(sessionId, request);
        var result = await sender.Send(command);
        return result.Match(Results.NoContent);
    }

    private static async Task<IResult> UpdateSessionStatus(
        [FromRoute] Guid sessionId,
        [FromBody] UpdateSessionStatusRequest request,
        ISender sender)
    {
        var command = new UpdateSessionStatusCommand(sessionId, request.Status, request.ConfirmRemainingNoShows);
        var result = await sender.Send(command);
        return result.Match(Results.NoContent);
    }

    private static async Task<IResult> DeleteSession(
        [FromRoute] Guid sessionId,
        ISender sender)
    {
        var command = new DeleteSessionCommand(sessionId);
        var result = await sender.Send(command);
        return result.Match(Results.NoContent);
    }

    // --- FR08, FR09, FR10 Handlers ---
    private static async Task<IResult> VoteBooking(
        [FromRoute] Guid sessionId,
        ISender sender)
    {
        var command = new VoteBookingCommand(sessionId);
        var result = await sender.Send(command);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> CancelBooking(
        [FromRoute] Guid sessionId,
        ISender sender)
    {
        var command = new CancelBookingCommand(sessionId);
        var result = await sender.Send(command);
        return result.Match(Results.NoContent);
    }

    private static async Task<IResult> GetSessionBookings(
        [FromRoute] Guid sessionId,
        ISender sender)
    {
        var query = new GetSessionBookingsQuery(sessionId);
        var result = await sender.Send(query);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> GetMyBookings(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetMyBookingsQuery();
        var result = await sender.Send(query, cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> GetMyBooking(
        [FromRoute] Guid sessionId,
        ISender sender)
    {
        var query = new GetMyBookingQuery(sessionId);
        var result = await sender.Send(query);
        return result.Match(booking => booking != null ? Results.Ok(booking) : Results.NotFound());
    }

    private static async Task<IResult> ConfirmBooking(
        [FromRoute] Guid sessionId,
        [FromRoute] Guid bookingId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new ConfirmBookingCommand(sessionId, bookingId);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: booking => Results.Ok(booking));
    }

    // --- FR11 Handlers ---
    private static async Task<IResult> CreateRecruitment(
        [FromRoute] Guid sessionId,
        [FromBody] CreateRecruitmentRequest request,
        ISender sender)
    {
        var command = new CreateRecruitmentCommand(sessionId, request);
        var result = await sender.Send(command);
        return result.Match(id => Results.Created($"/api/v1/sessions/{sessionId}/recruitments", new { RecruitmentId = id }));
    }

    private static async Task<IResult> GetSessionRecruitment(
        [FromRoute] Guid sessionId,
        ISender sender)
    {
        var query = new GetSessionRecruitmentQuery(sessionId);
        var result = await sender.Send(query);
        return result.Match(recruitment => recruitment != null ? Results.Ok(recruitment) : Results.NotFound());
    }

    private static async Task<IResult> DeleteRecruitment(
        [FromRoute] Guid sessionId,
        ISender sender)
    {
        var command = new DeleteRecruitmentCommand(sessionId);
        var result = await sender.Send(command);
        return result.Match(Results.NoContent);
    }

    // --- FR12 Handlers ---
    private static async Task<IResult> ExploreRecruitments(
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] double? radiusKm,
        [FromQuery] SkillLevel? minSkillLevel,
        ISender sender)
    {
        var query = new ExploreRecruitmentsQuery(latitude, longitude, radiusKm ?? 10, minSkillLevel);
        var result = await sender.Send(query);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> JoinRecruitment(
        [FromRoute] Guid recruitmentId,
        ISender sender)
    {
        var command = new JoinRecruitmentCommand(recruitmentId);
        var result = await sender.Send(command);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> CancelRecruitmentBooking(
        [FromRoute] Guid recruitmentId,
        ISender sender)
    {
        var command = new CancelRecruitmentBookingCommand(recruitmentId);
        var result = await sender.Send(command);
        return result.Match(Results.NoContent);
    }

    // --- FR14 Handlers ---
    private static async Task<IResult> GetSessionAttendance(
        [FromRoute] Guid sessionId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetSessionAttendanceQuery(sessionId);
        var result = await sender.Send(query, cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> UpdateSessionAttendance(
        [FromRoute] Guid sessionId,
        [FromRoute] Guid userId,
        [FromBody] UpdateAttendanceRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UpdateAttendanceCommand(sessionId, userId, request.Status);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> AddSessionWalkIn(
        [FromRoute] Guid sessionId,
        [FromBody] AddWalkInRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new AddWalkInCommand(sessionId, request.UserId);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> CompleteSessionAttendance(
        [FromRoute] Guid sessionId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new CompleteAttendanceCommand(sessionId);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Results.Ok);
    }

    // --- FR15 Handlers ---
    private static async Task<IResult> GetRotation(
        [FromRoute] Guid sessionId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetRotationQuery(sessionId);
        var result = await sender.Send(query, cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> InitializeRotation(
        [FromRoute] Guid sessionId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new InitializeRotationCommand(sessionId);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> GenerateRotationProposals(
        [FromRoute] Guid sessionId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new GenerateRotationProposalsCommand(sessionId);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> StartRotationMatch(
        [FromRoute] Guid sessionId,
        [FromRoute] Guid matchId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new StartRotationMatchCommand(sessionId, matchId);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> FinishRotationMatch(
        [FromRoute] Guid sessionId,
        [FromRoute] Guid matchId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new FinishRotationMatchCommand(sessionId, matchId);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Results.Ok);
    }

    // --- FR16 Handlers ---
    private static async Task<IResult> ConfirmManualMatch(
        [FromRoute] Guid sessionId,
        [FromRoute] Guid matchId,
        [FromBody] ConfirmManualMatchRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new ConfirmManualMatchCommand(sessionId, matchId, request.ExpectedRotationVersion);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> SetRotationPause(
        [FromRoute] Guid sessionId,
        [FromRoute] Guid userId,
        [FromBody] SetRotationPauseRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new SetRotationPauseCommand(sessionId, userId, request.IsPaused,
            request.ExpectedRotationVersion, request.ReplacementUserId);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> RestoreManualLineup(
        [FromRoute] Guid sessionId,
        [FromRoute] Guid matchId,
        [FromBody] RestoreManualLineupRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new RestoreManualLineupCommand(sessionId, matchId, request.ExpectedRotationVersion);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> UpdateManualLineup(
        [FromRoute] Guid sessionId,
        [FromRoute] Guid matchId,
        [FromBody] UpdateManualLineupRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UpdateManualLineupCommand(sessionId, matchId, request.TeamA,
            request.TeamB, request.ExpectedRotationVersion);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    // --- FR17 Handlers ---
    private static async Task<IResult> GetMyMatches(
        [FromRoute] Guid sessionId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetMyMatchesQuery(sessionId);
        var result = await sender.Send(query, cancellationToken);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> PauseMyMatches(
        [FromRoute] Guid sessionId,
        [FromBody] PauseMyMatchesRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new PauseMyMatchesCommand(sessionId, request.ExpectedRotationVersion);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> ResumeMyMatches(
        [FromRoute] Guid sessionId,
        [FromBody] ResumeMyMatchesRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new ResumeMyMatchesCommand(sessionId, request.ExpectedRotationVersion);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    // --- FR18 - FR20 Handlers ---

    private static async Task<IResult> GetSettlementForm(
        [FromRoute] Guid sessionId, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSettlementFormQuery(sessionId), cancellationToken);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> PreviewSettlement(
        [FromRoute] Guid sessionId, [FromBody] PreviewSettlementRequest request,
        ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new PreviewSettlementQuery(sessionId, request), cancellationToken);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> ApproveSettlement(
        [FromRoute] Guid sessionId, [FromBody] ApproveSettlementRequest request,
        ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ApproveSettlementCommand(sessionId, request.Input, request.PreviewToken), cancellationToken);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> GetSessionSettlement(
        [FromRoute] Guid sessionId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetSessionSettlementQuery(sessionId);
        var result = await sender.Send(query, cancellationToken);
        return result.Match(Results.Ok);
    }

    private static async Task<IResult> MarkMemberSettlementPaid(
        [FromRoute] Guid sessionId,
        [FromRoute] Guid userId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new MarkMemberPaidCommand(sessionId, userId);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(Results.Ok);
    }
}
