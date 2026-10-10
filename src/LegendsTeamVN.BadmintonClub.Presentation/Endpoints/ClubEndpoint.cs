using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Create;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.GetById;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.GetMembers;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.GetMyClubs;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Join;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.RemoveMember;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.SwitchContext;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateMemberRole;
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

    protected override void Map(RouteGroupBuilder group)
    {
        group.MapPost("", CreateClub)
            .WithName("CreateClub")
            .WithSummary("Tạo một câu lạc bộ mới (Người tạo tự động thành Host)")
            .RequireAuthorization();

        group.MapGet("my-clubs", GetMyClubs)
            .WithName("GetMyClubs")
            .WithSummary("Lấy danh sách các câu lạc bộ người dùng hiện tại đang tham gia và vai trò (FR03)")
            .RequireAuthorization();

        group.MapGet("{id:guid}", GetClubById)
            .WithName("GetClubById")
            .WithSummary("Xem thông tin chi tiết câu lạc bộ")
            .RequireAuthorization();

        group.MapPost("join", JoinClub)
            .WithName("JoinClub")
            .WithSummary("Tham gia câu lạc bộ bằng mã Code với vai trò mong muốn (FR03)")
            .RequireAuthorization();

        group.MapGet("{id:guid}/members", GetClubMembers)
            .WithName("GetClubMembers")
            .WithSummary("Xem danh sách thành viên và vai trò trong câu lạc bộ (FR03)")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Member.View);

        group.MapPut("{id:guid}/members/{userId:guid}/role", UpdateMemberRole)
            .WithName("UpdateMemberRole")
            .WithSummary("Cập nhật vai trò của thành viên trong câu lạc bộ")
            .RequireGroupPermission(Domain.Constants.GroupPermissions.Member.RoleManage);

        group.MapDelete("{id:guid}/members/{userId:guid}", RemoveMember)
            .WithName("RemoveClubMember")
            .WithSummary("Rời khỏi câu lạc bộ hoặc xóa thành viên khỏi nhóm")
            .RequireAuthorization();

        group.MapPost("{id:guid}/switch-context", SwitchContext)
            .WithName("SwitchClubContext")
            .WithSummary("Chuyển đổi câu lạc bộ đang hoạt động, trả về Token đồng bộ vai trò & quyền hạn (FR04)")
            .RequireAuthorization();
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
}
