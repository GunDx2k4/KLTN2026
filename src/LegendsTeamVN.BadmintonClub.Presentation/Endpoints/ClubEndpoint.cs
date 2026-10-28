using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Create;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Explore;
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
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Schedules.Update;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateInfo;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateMemberRole;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.FeeRule.Get;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.FeeRule.Update;
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
        var fr06Group = group.MapGroup("").WithTags("FR06 - Khám phá nhóm theo vị trí");
        var fr07Group = group.MapGroup("").WithTags("FR07 - Thông tin chi tiết & Lịch sinh hoạt nhóm");

        // --- GENERAL CLUB APIS ---
        generalGroup.MapPost("", CreateClub)
            .WithName("CreateClub")
            .WithSummary("Tạo một câu lạc bộ mới (Người tạo tự động thành Host)")
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
        fr03Group.MapGet("permissions", GetAllClubPermissions)
            .WithName("GetAllClubPermissions")
            .WithSummary("Lấy danh sách tất cả các quyền hạn chuẩn của câu lạc bộ (FR03)")
            .RequireAuthorization();

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
            .WithSummary("Xem danh sách các yêu cầu xin tham gia câu lạc bộ (FR03)")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Member.JoinRequestManage);

        fr03Group.MapPut("{id:guid}/join-requests/{requestId:guid}/approve", ApproveJoinRequest)
            .WithName("ApproveJoinRequest")
            .WithSummary("Duyệt yêu cầu xin tham gia câu lạc bộ (FR03)")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Member.JoinRequestManage);

        fr03Group.MapPut("{id:guid}/join-requests/{requestId:guid}/reject", RejectJoinRequest)
            .WithName("RejectJoinRequest")
            .WithSummary("Từ chối yêu cầu xin tham gia câu lạc bộ (FR03)")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Member.JoinRequestManage);

        // --- FR06: KHÁM PHÁ NHÓM THEO VỊ TRÍ GPS & BÁN KÍNH ---
        fr06Group.MapGet("explore", ExploreClubs)
            .WithName("ExploreClubs")
            .WithSummary("Quét tìm các câu lạc bộ cầu lông xung quanh vị trí GPS (FR06)")
            .WithDescription("Tìm kiếm các câu lạc bộ lân cận dựa trên tọa độ GPS, bán kính km, trình độ và trạng thái tuyển thành viên.")
            .RequireAuthorization();

        // --- FR07: THÔNG TIN CHI TIẾT & LỊCH SINH HOẠT NHÓM ---
        fr07Group.MapGet("{id:guid}", GetClubById)
            .WithName("GetClubById")
            .WithSummary("Xem thông tin chi tiết đầy đủ câu lạc bộ, sân bãi và lịch sinh hoạt (FR07)")
            .WithDescription("Trả về thông tin chi tiết CLB, sân cố định, lịch sinh hoạt hàng tuần, thông tin Host và trạng thái gia nhập của user hiện tại.")
            .RequireAuthorization();

        fr07Group.MapPut("{id:guid}/info", UpdateClubInfo)
            .WithName("UpdateClubInfo")
            .WithSummary("Host cập nhật thông tin chi tiết sân bãi, tọa độ GPS và tiêu chuẩn tuyển chọn của CLB (FR07)")
            .WithDescription("Host hoặc Ban quản trị nhóm cấu hình sân cố định, địa chỉ, GPS, khoảng trình độ và bật/tắt mở tuyển.")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Member.InfoUpdate);

        fr07Group.MapPut("{id:guid}/schedules", UpdateClubSchedules)
            .WithName("UpdateClubSchedules")
            .WithSummary("Host thiết lập lịch sinh hoạt định kỳ lặp lại hàng tuần của nhóm (FR07)")
            .WithDescription("Thiết lập các khung giờ sinh hoạt cố định trong tuần của CLB.")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Operations.ScheduleManage);

        // --- FR19: CẤU HÌNH BIỂU PHÍ & QUY TẮC CHIA TIỀN ---
        var fr19Group = group.MapGroup("").WithTags("FR19 - Hạch toán biểu phí và Quỹ nhóm");

        fr19Group.MapGet("{id:guid}/fee-rule", GetClubFeeRule)
            .WithName("GetClubFeeRule")
            .WithSummary("Xem cấu hình biểu phí và mức ưu đãi của câu lạc bộ (FR19)")
            .WithDescription("Trả về quy tắc tính phí hội viên tháng, hội viên ngày, khách vãng lai và các mức ưu đãi Nữ.")
            .RequireAuthorization();

        fr19Group.MapPut("{id:guid}/fee-rule", UpdateClubFeeRule)
            .WithName("UpdateClubFeeRule")
            .WithSummary("Host/Thủ quỹ cập nhật cấu hình biểu phí và ưu đãi của câu lạc bộ (FR19)")
            .WithDescription("Cập nhật chế độ tính phí, tiền tháng, tiền ngày, tiền khách vãng lai, ưu đãi Nữ và quy tắc làm tròn.")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Finance.BankManage);
    }

    // --- FR06 Handlers ---
    private static async Task<IResult> ExploreClubs(
        [FromQuery] double? latitude,
        [FromQuery] double? longitude,
        [FromQuery] double? radiusKm,
        [FromQuery] SkillLevel? skillLevel,
        [FromQuery] bool? isRecruiting,
        [FromQuery] string? searchTerm,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new ExploreClubsQuery(latitude, longitude, radiusKm ?? 10, skillLevel, isRecruiting, searchTerm);
        var result = await sender.Send(query, cancellationToken);
        return result.Match(onSuccess: clubs => Results.Ok(clubs));
    }

    // --- FR07 Handlers ---
    private static async Task<IResult> GetClubById(
        [FromRoute] Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetClubByIdQuery(id);
        var result = await sender.Send(query, cancellationToken);
        return result.Match(onSuccess: club => Results.Ok(club));
    }

    private static async Task<IResult> UpdateClubInfo(
        [FromRoute] Guid id,
        [FromBody] UpdateClubInfoRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UpdateClubInfoCommand(id, request);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: () => Results.NoContent());
    }

    private static async Task<IResult> UpdateClubSchedules(
        [FromRoute] Guid id,
        [FromBody] UpdateClubSchedulesRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UpdateClubSchedulesCommand(id, request);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: () => Results.NoContent());
    }

    // --- General & FR03 Handlers ---
    private static async Task<IResult> CreateClub(
        [FromBody] CreateClubRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new CreateClubCommand(
            request.Name,
            request.Description,
            request.AvatarUrl,
            request.FixedVenueName,
            request.FixedAddress,
            request.Lat,
            request.Lng,
            request.MinSkillLevel,
            request.MaxSkillLevel,
            request.Schedules);
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
        var command = new UpdateMemberRoleCommand(id, userId, request.RoleId, request.Role, request.RoleName);
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

    private static async Task<IResult> GetAllClubPermissions(
        IClubRepository clubRepository,
        CancellationToken cancellationToken)
    {
        var permissions = await clubRepository.GetAllPermissionsAsync(cancellationToken);
        var response = permissions.Select(p => new ClubRolePermissionItemResponse(
            p.Id,
            p.PermissionCode,
            p.PermissionName,
            p.ModuleGroup.ToString()
        )).ToList();
        return Results.Ok(response);
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

    // --- FR19 Handlers ---
    private static async Task<IResult> GetClubFeeRule(
        [FromRoute] Guid id,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var query = new GetClubFeeRuleQuery(id);
        var result = await sender.Send(query, cancellationToken);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> UpdateClubFeeRule(
        [FromRoute] Guid id,
        [FromBody] UpdateClubFeeRuleRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var command = new UpdateClubFeeRuleCommand(id, request);
        var result = await sender.Send(command, cancellationToken);
        return result.Match(onSuccess: data => Results.Ok(data));
    }
}
