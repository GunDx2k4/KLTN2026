using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetReviewInvitations;
using LegendsTeamVN.BadmintonClub.Application.Features.Reputation.SubmitSessionReview;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Users.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Notifications.GetMy;
using LegendsTeamVN.BadmintonClub.Application.Features.Notifications.MarkAllRead;
using LegendsTeamVN.BadmintonClub.Application.Features.Notifications.MarkRead;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.Create;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.Delete;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.GetAllPermissions;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.GetById;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.GetList;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.GetPermissions;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.Me;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.Profile.Get;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.Profile.Update;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.ResetPassword;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.ToggleLock;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.Update;
using LegendsTeamVN.BadmintonClub.Application.Features.Users.UpdateRoles;
using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints;

public class UserEndpoint : EndpointGroupBase
{
    protected override string Name => "user";
    protected override string? Tag => null;

    protected override void Map(RouteGroupBuilder group)
    {
        var reviews = group.MapGroup("me/reviews").WithTags("FR23 - Đánh giá buổi chơi").RequireAuthorization();
        reviews.MapGet("invitations", GetReviewInvitations)
            .Produces<ReputationPageResponse<ReviewInvitationResponse>>(200).WithName("GetReviewInvitations");
        reviews.MapPost("", SubmitSessionReview)
            .Produces<SessionReviewResponse>(200).ProducesProblem(400).ProducesProblem(403).ProducesProblem(404)
            .ProducesProblem(409).WithName("SubmitSessionReview");
        var payments = group.MapGroup("me/fees").WithTags("UC17 - Khoản đóng của tôi").RequireAuthorization("SessionOperations");
        payments.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        payments.MapGet("", ListMyFees).WithName("ListMyFees");
        payments.MapGet("{feeId:guid}", GetMyFee).WithName("GetMyFee");
        payments.MapGet("{feeId:guid}/qr.png", GetMyFeeQrImage).WithName("GetMyFeeQrImage");
        payments.MapPost("{feeId:guid}/cash-request", RequestCashPayment).WithName("RequestCashPayment");
        payments.MapPost("{feeId:guid}/bank-transfer", PrepareBankTransfer).WithName("PrepareBankTransfer");
        payments.MapPost("{feeId:guid}/transfer-notice", ReportBankTransfer).WithName("ReportBankTransfer");

        var notifGroup = group.MapGroup("me/notifications").WithTags("Notifications").RequireAuthorization();
        notifGroup.MapGet("", GetMyNotifications).WithName("GetMyNotifications");
        notifGroup.MapPut("{id:guid}/read", MarkNotificationAsRead).WithName("MarkNotificationAsRead");
        notifGroup.MapPut("read-all", MarkAllNotificationsAsRead).WithName("MarkAllNotificationsAsRead");

        var usersGroup = group.MapGroup("").WithTags("Users");
        var fr05Group = group.MapGroup("").WithTags("FR05 - Quản lý hồ sơ cá nhân & Trình độ");

        // --- FR05: QUẢN LÝ HỒ SƠ CÁ NHÂN & TRÌNH ĐỘ ---
        fr05Group.MapGet("profile", GetProfile)
             .WithName("GetUserProfile")
             .WithSummary("Xem thông tin chi tiết hồ sơ cá nhân và trình độ chuyên môn (FR05)")
             .WithDescription("Trả về thông tin hồ sơ cá nhân, trình độ tự đánh giá (NB, Y, TB, K, G), ảnh đại diện và điểm uy tín.")
             .RequireAuthorization();

        fr05Group.MapPut("profile", UpdateProfile)
             .WithName("UpdateUserProfile")
             .WithSummary("Cập nhật họ tên, giới tính, ảnh đại diện và trình độ chuyên môn tự đánh giá (FR05)")
             .WithDescription("Cập nhật hồ sơ cá nhân của người dùng hiện tại.")
             .RequireAuthorization();

        // --- USERS MANAGEMENT (IDENTITY) ---
        usersGroup.MapGet("me", GetMe)
             .WithName("GetMe")
             .WithSummary("Get current user details")
             .WithDescription("Returns details, roles, and tree permissions of the currently authenticated user.")
             .RequireAuthorization();

        usersGroup.MapGet("permissions", GetMyPermissions)
             .WithName("GetMyPermissions")
             .WithSummary("Get current user permissions")
             .WithDescription("Returns roles and grouped tree permissions of the currently authenticated user.")
             .RequireAuthorization();

        usersGroup.MapGet("all-permissions", GetAllPermissions)
             .WithName("GetAllPermissions")
             .WithSummary("Get all system permissions")
             .WithDescription("Returns all system permissions grouped in a multi-level tree hierarchy.")
             .RequirePermission(AppPermissions.Users.Read);

        usersGroup.MapGet("", GetUsers)
             .WithName("GetUsers")
             .WithSummary("Gets all users with pagination")
             .WithDescription("Retrieves a paged list of all users based on filter criteria.")
             .RequirePermission(AppPermissions.Users.Read);

        usersGroup.MapGet("{id:guid}", GetUserById)
             .WithName("GetUserById")
             .WithSummary("Get user details by ID")
             .WithDescription("Retrieves details, roles, and permissions of a specific user by ID.")
             .RequirePermission(AppPermissions.Users.Read);

        usersGroup.MapPost("", CreateUser)
             .WithName("CreateUser")
             .WithSummary("Create user")
             .WithDescription("Creates a new user account.")
             .RequirePermission(AppPermissions.Users.Create);

        usersGroup.MapPut("{id:guid}", UpdateUser)
             .WithName("UpdateUser")
             .WithSummary("Update user")
             .WithDescription("Updates user email, username, or phone number.")
             .RequirePermission(AppPermissions.Users.Update);

        usersGroup.MapDelete("{id:guid}", DeleteUser)
             .WithName("DeleteUser")
             .WithSummary("Delete user")
             .WithDescription("Deletes a user account.")
             .RequirePermission(AppPermissions.Users.Delete);

        usersGroup.MapPut("{id:guid}/lock", ToggleUserLock)
             .WithName("ToggleUserLock")
             .WithSummary("Lock or unlock user")
             .WithDescription("Locks or unlocks a user account.")
             .RequirePermission(AppPermissions.Users.Lock);

        usersGroup.MapPut("{id:guid}/roles", UpdateUserRoles)
             .WithName("UpdateUserRoles")
             .WithSummary("Assign roles to user")
             .WithDescription("Assigns roles to a user account.")
             .RequirePermission(AppPermissions.Users.AssignRoles);

        usersGroup.MapPost("{id:guid}/reset-password", ResetUserPassword)
             .WithName("ResetUserPassword")
             .WithSummary("Reset user password")
             .WithDescription("Resets password for a user account.")
             .RequirePermission(AppPermissions.Users.ResetPassword);
    }

    // --- FR05 Handlers ---
    private static async Task<IResult> GetReviewInvitations(ISender sender, CancellationToken ct, int page = 1, int pageSize = 20)
    {
        var result = await sender.Send(new GetReviewInvitationsQuery(page, pageSize), ct);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> SubmitSessionReview(SubmitSessionReviewRequest request, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new SubmitSessionReviewCommand(request.SessionId, request.Rating, request.Comment), ct);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> ListMyFees(ISender sender, CancellationToken ct, int page = 1, int pageSize = 20,
        Guid? groupId = null, Guid? sessionId = null, Domain.Enums.PaymentStatus? paymentStatus = null)
    {
        var result = await sender.Send(new Application.Features.Payments.ListMine.ListMyFeesQuery(page, pageSize, groupId, sessionId, paymentStatus), ct);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> GetMyFee(Guid feeId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new Application.Features.Payments.GetMine.GetMyFeeQuery(feeId), ct);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> GetMyFeeQrImage(Guid feeId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new Application.Features.Payments.GetMine.GetMyFeeQrImageQuery(feeId), ct);
        return result.Match(onSuccess: data => Results.File(data, "image/png"));
    }

    private static async Task<IResult> RequestCashPayment(Guid feeId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new Application.Features.Payments.CashRequest.RequestCashPaymentCommand(feeId), ct);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> PrepareBankTransfer(Guid feeId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new Application.Features.Payments.BankTransfer.PrepareBankTransferCommand(feeId), ct);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> ReportBankTransfer(Guid feeId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new Application.Features.Payments.TransferNotice.ReportBankTransferCommand(feeId), ct);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> GetMyNotifications(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetMyNotificationsQuery(), ct);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> MarkNotificationAsRead(Guid id, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new MarkNotificationAsReadCommand(id), ct);
        return result.Match(onSuccess: () => Results.NoContent());
    }

    private static async Task<IResult> MarkAllNotificationsAsRead(ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new MarkAllNotificationsAsReadCommand(), ct);
        return result.Match(onSuccess: () => Results.NoContent());
    }

    private static async Task<IResult> GetProfile(ISender sender)
    {
        var result = await sender.Send(new GetUserProfileQuery());
        return result.Match(onSuccess: response => Results.Ok(response));
    }

    private static async Task<IResult> UpdateProfile([FromBody] UpdateUserProfileRequest request, ISender sender)
    {
        var result = await sender.Send(new UpdateUserProfileCommand(request));
        return result.Match(onSuccess: () => Results.NoContent());
    }

    // --- User Handlers ---
    private static async Task<IResult> GetMe(ISender sender)
    {
        var result = await sender.Send(new MeQuery());
        return result.Match(onSuccess: response => Results.Ok(response));
    }

    private static async Task<IResult> GetMyPermissions(ISender sender)
    {
        var result = await sender.Send(new GetMyPermissionsQuery());
        return result.Match(onSuccess: response => Results.Ok(response));
    }

    private static async Task<IResult> GetAllPermissions(ISender sender)
    {
        var result = await sender.Send(new GetAllPermissionsQuery());
        return result.Match(onSuccess: response => Results.Ok(response));
    }

    private static async Task<IResult> GetUsers([AsParameters] GetUsersRequest request, ISender sender)
    {
        var result = await sender.Send(new GetUsersQuery(request));
        return result.Match(onSuccess: response => Results.Ok(response));
    }

    private static async Task<IResult> GetUserById(Guid id, ISender sender)
    {
        var result = await sender.Send(new GetUserByIdQuery(id));
        return result.Match(onSuccess: response => Results.Ok(response));
    }

    private static async Task<IResult> CreateUser(CreateUserCommand command, ISender sender)
    {
        var result = await sender.Send(command);
        return result.Match(onSuccess: userId => Results.Created($"/api/v1/user/{userId}", userId));
    }

    private static async Task<IResult> UpdateUser(Guid id, UpdateUserRequest request, ISender sender)
    {
        var result = await sender.Send(new UpdateUserCommand(id, request.Email, request.UserName, request.PhoneNumber));
        return result.Match(onSuccess: () => Results.NoContent());
    }

    private static async Task<IResult> DeleteUser(Guid id, ISender sender)
    {
        var result = await sender.Send(new DeleteUserCommand(id));
        return result.Match(onSuccess: () => Results.NoContent());
    }

    private static async Task<IResult> ToggleUserLock(Guid id, ToggleUserLockRequest request, ISender sender)
    {
        var result = await sender.Send(new ToggleUserLockCommand(id, request.IsLocked));
        return result.Match(onSuccess: () => Results.NoContent());
    }

    private static async Task<IResult> UpdateUserRoles(Guid id, UpdateUserRolesRequest request, ISender sender)
    {
        var result = await sender.Send(new UpdateUserRolesCommand(id, request.Roles));
        return result.Match(onSuccess: () => Results.NoContent());
    }

    private static async Task<IResult> ResetUserPassword(Guid id, ResetUserPasswordRequest request, ISender sender)
    {
        var result = await sender.Send(new ResetUserPasswordCommand(id, request.NewPassword));
        return result.Match(onSuccess: () => Results.NoContent());
    }
}

public record UpdateUserRequest(string Email, string? UserName, string? PhoneNumber);
public record ToggleUserLockRequest(bool IsLocked);
public record ResetUserPasswordRequest(string NewPassword);
