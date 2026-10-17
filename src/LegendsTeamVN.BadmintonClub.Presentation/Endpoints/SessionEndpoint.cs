using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Attendance.AddWalkIn;
using LegendsTeamVN.BadmintonClub.Application.Features.Attendance.Complete;
using LegendsTeamVN.BadmintonClub.Application.Features.Attendance.GetList;
using LegendsTeamVN.BadmintonClub.Application.Features.Attendance.Update;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Cancel;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.GetList;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.GetMyBooking;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.GetMyBookings;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Vote;
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
        var fr08fr10Group = group.MapGroup("").WithTags("FR08-FR10 - Biểu quyết giữ chỗ & Hàng đợi");
        var fr11Group = group.MapGroup("").WithTags("FR11 - Phát tín hiệu tìm khách giao lưu");
        var fr12Group = group.MapGroup("").WithTags("FR12 - Khách vãng lai đăng ký tham gia");
        var fr14Group = group.MapGroup("").WithTags("FR14 - Điểm danh tại sân");

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
        // --- FR08, FR09, FR10: BIỂU QUYẾT GIỮ CHỖ & HÀNG ĐỢI TỰ ĐỘNG ---
        // ==============================================================
        fr08fr10Group.MapPost("{sessionId:guid}/bookings", VoteBooking)
            .WithName("VoteBooking")
            .WithSummary("Biểu quyết giữ chỗ tham gia buổi chơi (Tự động vào hàng đợi nếu đầy) (FR08, FR09)")
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
            .WithSummary("Xem danh sách thành viên đã biểu quyết giữ chỗ và hàng đợi chờ (FR08, FR09)")
            .RequireAuthorization();

        fr08fr10Group.MapGet("{sessionId:guid}/bookings/me", GetMyBooking)
            .WithName("GetMyBooking")
            .WithSummary("Kiểm tra trạng thái vé biểu quyết giữ chỗ của cá nhân (FR08)")
            .RequireAuthorization();

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
            .WithSummary("Khách vãng lai quét tìm các kèo giao lưu quanh tọa độ GPS (FR12)")
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
            .RequireAuthorization();

        fr14Group.MapPut("{sessionId:guid}/attendance/{userId:guid}", UpdateSessionAttendance)
            .WithName("UpdateSessionAttendance")
            .WithSummary("Đánh dấu có mặt hoặc không đến sân cho người chơi (FR14)")
            .Produces<SessionAttendanceResponse>()
            .RequireAuthorization();

        fr14Group.MapPost("{sessionId:guid}/attendance/walk-ins", AddSessionWalkIn)
            .WithName("AddSessionWalkIn")
            .WithSummary("Thêm nhanh người chơi có tài khoản và xác nhận có mặt (FR14)")
            .Produces<SessionAttendanceResponse>()
            .RequireAuthorization();

        fr14Group.MapPost("{sessionId:guid}/attendance/complete", CompleteSessionAttendance)
            .WithName("CompleteSessionAttendance")
            .WithSummary("Hoàn tất điểm danh, cung cấp danh sách sẵn sàng cho điều phối (FR14)")
            .Produces<SessionAttendanceResponse>()
            .RequireAuthorization();
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
}
