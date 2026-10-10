using LegendsTeamVN.BadmintonClub.Application.DTOs.Auth;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Caching;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.DTOs;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Auth.Login;

public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IDeviceRepository deviceRepository,
    IUserManagerService userManagerService,
    IJwtTokenService jwtTokenService,
    ICacheService cacheService) : ICommandHandler<LoginCommand, LoginResponse>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // 1. Tìm người dùng trong Domain qua số điện thoại hoặc email
        var domainUser = await userRepository.FindByEmailOrPhoneAsync(request.Identifier, cancellationToken);
        if (domainUser == null || !domainUser.IsActive)
        {
            return Result.Failure<LoginResponse>(
                Error.Unauthorized("User.Unauthorized", "Tài khoản hoặc mật khẩu không chính xác hoặc đã bị vô hiệu hóa."));
        }

        // 2. So khớp mật khẩu với Identity
        var identifierToVerify = domainUser.EmailAddress ?? domainUser.PhoneNumber ?? request.Identifier;
        var (succeeded, identityUserId) = await userManagerService.CheckPasswordAsync(identifierToVerify, request.Password);

        if (!succeeded || identityUserId == null)
        {
            return Result.Failure<LoginResponse>(
                Error.Unauthorized("User.Unauthorized", "Tài khoản hoặc mật khẩu không chính xác."));
        }

        // 3. Cấp phát phiên an toàn (JWT AccessToken & RefreshToken)
        var roles = await userManagerService.GetRolesAsync(identityUserId.Value);
        var permissions = await userManagerService.GetPermissionsAsync(identityUserId.Value);

        var emailOrUser = domainUser.EmailAddress ?? domainUser.PhoneNumber ?? request.Identifier;
        var accessToken = jwtTokenService.GenerateAccessToken(identityUserId.Value, emailOrUser, domainUser.FullName, roles, null, permissions);
        var refreshToken = jwtTokenService.GenerateRefreshToken();
        var refreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        var authResponse = new AuthenticationResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            RefreshTokenExpiryTime = refreshTokenExpiryTime
        };
        await cacheService.SetAsync(emailOrUser, authResponse, TimeSpan.FromDays(7), cancellationToken);

        // 4. Cập nhật thiết bị nhận thông báo qua DeviceRepository (nếu có deviceToken)
        if (!string.IsNullOrWhiteSpace(request.DeviceToken))
        {
            var platform = Enum.TryParse<DevicePlatform>(request.Platform, true, out var parsedPlatform) ? parsedPlatform : (DevicePlatform?)null;
            await deviceRepository.UpsertDeviceAsync(domainUser.Id, request.DeviceToken, platform, cancellationToken);
        }

        // 5. Truy vấn danh sách CLB / hội nhóm người dùng đang tham gia
        var userWithClubs = await userRepository.GetUserWithMembershipsAsync(domainUser.Id, cancellationToken);
        var clubs = userWithClubs?.GroupMemberships?
            .Select(m => new UserClubMembershipResponse(
                ClubId: m.GroupId,
                ClubName: m.Group?.GroupName ?? string.Empty,
                ClubCode: m.Group?.Code ?? string.Empty,
                AvatarUrl: m.Group?.AvatarUrl,
                RoleName: m.RoleEntity?.RoleName ?? string.Empty,
                MemberType: m.MemberType,
                Status: m.Status.ToString()
            ))
            .ToList() ?? [];

        // 6. Trả về LoginResponse
        var response = new LoginResponse(
            UserId: domainUser.Id,
            FullName: domainUser.FullName,
            Email: domainUser.EmailAddress,
            PhoneNumber: domainUser.PhoneNumber,
            AvatarUrl: domainUser.AvatarUrl,
            Gender: domainUser.Gender,
            SkillLevel: domainUser.SkillLevel.ToString(),
            ReputationScore: domainUser.ReputationScore,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            RefreshTokenExpiryTime: refreshTokenExpiryTime,
            Clubs: clubs
        );

        return Result.Success(response);
    }
}
