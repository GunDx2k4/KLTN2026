using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Create;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.GetById;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.GetMembers;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.GetMyClubs;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Join;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.Approve;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.Create;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.GetList;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.Reject;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.RemoveMember;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Roles.Create;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Roles.Delete;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Roles.GetList;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Roles.UpdatePermissions;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.SwitchContext;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateMemberRole;
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

public class ClubEndpoint : EndpointGroupBase
{
    protected override string Name => "clubs";
    protected override string? Tag => null;

    protected override void Map(RouteGroupBuilder group)
    {
        var generalGroup = group.MapGroup("").WithTags("Clubs");
        var fr03Group = group.MapGroup("").WithTags("FR03 - Quản lý CLB & Thành viên");

        // --- GENERAL CLUB APIS ---
        generalGroup.MapPost("", CreateClub)
            .WithName("CreateClub")
            .WithSummary("Tạo một câu lạc bộ mới (Người tạo tự động thành Host)")
            .RequireAuthorization();

        generalGroup.MapGet("{id:guid}", GetClubById)
            .WithName("GetClubById")
            .WithSummary("Xem thông tin chi tiết câu lạc bộ")
            .RequireAuthorization();

        generalGroup.MapPost("{id:guid}/switch-context", SwitchContext)
            .WithName("SwitchClubContext")
            .WithSummary("Chuyển đổi câu lạc bộ đang hoạt động, trả về Token đồng bộ vai trò & quyền hạn (FR04)")
            .RequireAuthorization();

        // --- FR03: MEMBERSHIP & ROLES MANAGEMENT ---
        fr03Group.MapGet("my-clubs", GetMyClubs)
            .WithName("GetMyClubs")
            .WithSummary("Lấy danh sách các câu lạc bộ người dùng hiện tại đang tham gia và vai trò (FR03)")
            .RequireAuthorization();

        fr03Group.MapPost("join", JoinClub)
            .WithName("JoinClub")
            .WithSummary("Tham gia câu lạc bộ bằng mã Code với vai trò mong muốn (FR03)")
            .RequireAuthorization();

        fr03Group.MapGet("{id:guid}/members", GetClubMembers)
            .WithName("GetClubMembers")
            .WithSummary("Xem danh sách thành viên và vai trò trong câu lạc bộ (FR03)")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Member.View);

        fr03Group.MapPut("{id:guid}/members/{userId:guid}/role", UpdateMemberRole)
            .WithName("UpdateMemberRole")
            .WithSummary("Cập nhật vai trò của thành viên trong câu lạc bộ (FR03)")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Member.RoleManage);

        fr03Group.MapDelete("{id:guid}/members/{userId:guid}", RemoveMember)
            .WithName("RemoveClubMember")
            .WithSummary("Rời khỏi câu lạc bộ hoặc xóa thành viên khỏi nhóm (FR03)")
            .RequireAuthorization();

        // --- GROUP ROLES MANAGEMENT (FR03) ---
        fr03Group.MapGet("{id:guid}/roles", GetClubRoles)
            .WithName("GetClubRoles")
            .WithSummary("Lấy danh sách các vai trò nội bộ của câu lạc bộ (FR03)")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Member.View);

        fr03Group.MapPost("{id:guid}/roles", CreateClubRole)
            .WithName("CreateClubRole")
            .WithSummary("Tạo vai trò tùy chỉnh mới cho câu lạc bộ (FR03)")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Member.RoleManage);

        fr03Group.MapPut("{id:guid}/roles/{roleId:guid}/permissions", UpdateClubRolePermissions)
            .WithName("UpdateClubRolePermissions")
            .WithSummary("Cập nhật danh sách quyền cho vai trò trong câu lạc bộ (FR03)")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Member.RoleManage);

        fr03Group.MapDelete("{id:guid}/roles/{roleId:guid}", DeleteClubRole)
            .WithName("DeleteClubRole")
            .WithSummary("Xóa vai trò tùy chỉnh khỏi câu lạc bộ (FR03)")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Member.RoleManage);

        // --- JOIN REQUESTS MANAGEMENT (FR03) ---
        fr03Group.MapPost("{id:guid}/join-requests", CreateJoinRequest)
            .WithName("CreateJoinRequest")
            .WithSummary("Gửi yêu cầu xin tham gia câu lạc bộ (FR03)")
            .RequireAuthorization();

        fr03Group.MapGet("{id:guid}/join-requests", GetJoinRequests)
            .WithName("GetJoinRequests")
            .WithSummary("Xem danh sách yêu cầu xin gia nhập câu lạc bộ (FR03)")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Member.JoinRequestManage);

        fr03Group.MapPut("{id:guid}/join-requests/{requestId:guid}/approve", ApproveJoinRequest)
            .WithName("ApproveJoinRequest")
            .WithSummary("Duyệt yêu cầu gia nhập câu lạc bộ và gán vai trò ban đầu (FR03)")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Member.JoinRequestManage);

        fr03Group.MapPut("{id:guid}/join-requests/{requestId:guid}/reject", RejectJoinRequest)
            .WithName("RejectJoinRequest")
            .WithSummary("Từ chối yêu cầu gia nhập câu lạc bộ (FR03)")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Member.JoinRequestManage);
    }

    private static async Task<IResult> CreateClub(
        [FromBody] CreateClubRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new CreateClubCommand(request.Name, request.Description, request.AvatarUrl);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: clubId => Results.Created($"/api/v1/clubs/{clubId}", clubId));
    }

    private static async Task<IResult> GetMyClubs(
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetMyClubsQuery();
        var result = await sender.Send(query, cancellationToken);
        return result.Match(onSuccess: clubs => Results.Ok(clubs));
    }

    private static async Task<IResult> GetClubById(
        [FromRoute] Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetClubByIdQuery(id);
        var result = await sender.Send(query, cancellationToken);
        return result.Match(onSuccess: club => Results.Ok(club));
    }

    private static async Task<IResult> JoinClub(
        [FromBody] JoinClubRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new JoinClubCommand(request.ClubCode, request.RoleId, request.Nickname);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: memberId => Results.Ok(new { MemberId = memberId }));
    }

    private static async Task<IResult> GetClubMembers(
        [FromRoute] Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetClubMembersQuery(id);
        var result = await sender.Send(query, cancellationToken);
        return result.Match(onSuccess: members => Results.Ok(members));
    }

    private static async Task<IResult> UpdateMemberRole(
        [FromRoute] Guid id,
        [FromRoute] Guid userId,
        [FromBody] UpdateMemberRoleRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UpdateMemberRoleCommand(id, userId, request.RoleId);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: success => Results.Ok(new { Success = success }));
    }

    private static async Task<IResult> RemoveMember(
        [FromRoute] Guid id,
        [FromRoute] Guid userId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new RemoveMemberCommand(id, userId);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: success => Results.Ok(new { Success = success }));
    }

    private static async Task<IResult> SwitchContext(
        [FromRoute] Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new SwitchClubContextCommand(id);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: response => Results.Ok(response));
    }

    private static async Task<IResult> GetClubRoles(
        [FromRoute] Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetClubRolesQuery(id);
        var result = await sender.Send(query, cancellationToken);
        return result.Match(onSuccess: roles => Results.Ok(roles));
    }

    private static async Task<IResult> CreateClubRole(
        [FromRoute] Guid id,
        [FromBody] CreateClubRoleRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new CreateClubRoleCommand(id, request.RoleName, request.Description, request.PermissionIds, request.IsDefault);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: roleId => Results.Created($"/api/v1/clubs/{id}/roles/{roleId}", new { RoleId = roleId }));
    }

    private static async Task<IResult> UpdateClubRolePermissions(
        [FromRoute] Guid id,
        [FromRoute] Guid roleId,
        [FromBody] UpdateClubRolePermissionsRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UpdateClubRolePermissionsCommand(id, roleId, request.PermissionIds);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: success => Results.Ok(new { Success = success }));
    }

    private static async Task<IResult> DeleteClubRole(
        [FromRoute] Guid id,
        [FromRoute] Guid roleId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new DeleteClubRoleCommand(id, roleId);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: success => Results.NoContent());
    }

    private static async Task<IResult> CreateJoinRequest(
        [FromRoute] Guid id,
        [FromBody] CreateJoinRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new CreateJoinRequestCommand(id, request.Introduction);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: requestId => Results.Created($"/api/v1/clubs/{id}/join-requests/{requestId}", new { RequestId = requestId }));
    }

    private static async Task<IResult> GetJoinRequests(
        [FromRoute] Guid id,
        [FromQuery] JoinRequestStatus? status,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetJoinRequestsQuery(id, status);
        var result = await sender.Send(query, cancellationToken);
        return result.Match(onSuccess: requests => Results.Ok(requests));
    }

    private static async Task<IResult> ApproveJoinRequest(
        [FromRoute] Guid id,
        [FromRoute] Guid requestId,
        [FromBody] ApproveJoinRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new ApproveJoinRequestCommand(id, requestId, request.RoleId, request.MemberType);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: memberId => Results.Ok(new { MemberId = memberId }));
    }

    private static async Task<IResult> RejectJoinRequest(
        [FromRoute] Guid id,
        [FromRoute] Guid requestId,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new RejectJoinRequestCommand(id, requestId);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: success => Results.Ok(new { Success = success }));
    }
}
