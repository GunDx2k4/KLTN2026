using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Auth;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Caching;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.DTOs;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Auth.ExternalRegister;

public sealed class ExternalRegisterCommandHandler(
    IExternalAuthService externalAuthService,
    IUserRepository userRepository,
    IUserManagerService userManagerService,
    IJwtTokenService jwtTokenService,
    ICacheService cacheService) : ICommandHandler<ExternalRegisterCommand, RegisterResponse>
{
    public async Task<Result<RegisterResponse>> Handle(ExternalRegisterCommand request, CancellationToken cancellationToken)
    {
        // 1. Xác thực chữ ký số ID Token từ bên thứ ba (Google / Firebase)
        var externalUserInfo = await externalAuthService.VerifyTokenAsync(request.IdToken, request.Provider, cancellationToken);
        if (externalUserInfo == null)
        {
            return Result.Failure<RegisterResponse>(
                Error.Unauthorized("Auth.InvalidExternalToken", "Token xác thực bên thứ ba không hợp lệ hoặc đã hết hạn."));
        }

        // 2. Tìm hoặc tạo tài khoản trong Identity (Authentication)
        var identityUser = await userManagerService.FindByExternalLoginAsync(externalUserInfo.Provider, externalUserInfo.ProviderKey);
        Guid userId;

        if (identityUser != null)
        {
            userId = identityUser.Id;
        }
        else
        {
            var (succeeded, createdUserId, errors) = await userManagerService.CreateExternalUserAsync(
                externalUserInfo.Provider,
                externalUserInfo.ProviderKey,
                externalUserInfo.Email,
                externalUserInfo.FullName
            );

            if (!succeeded || createdUserId == null)
            {
                return Result.Failure<RegisterResponse>(
                    Error.Validation("Auth.ExternalRegisterFailed", string.Join(", ", errors)));
            }

            userId = createdUserId.Value;
        }

        // 3. Đồng bộ hồ sơ người dùng trong Domain (app_user)
        var domainUser = await userRepository.FindByIdAsync(userId, cancellationToken);
        if (domainUser == null)
        {
            var existingByEmail = await userRepository.FindByEmailOrPhoneAsync(externalUserInfo.Email, cancellationToken);
            if (existingByEmail != null)
            {
                domainUser = existingByEmail;
            }
            else
            {
                domainUser = AppUser.CreateExternal(
                    id: userId,
                    fullName: externalUserInfo.FullName,
                    emailAddress: externalUserInfo.Email,
                    avatarUrl: externalUserInfo.AvatarUrl
                );

                userRepository.Add(domainUser);
            }
        }

        // 4. Phát hành JWT AccessToken & RefreshToken từ Identity
        var roles = await userManagerService.GetRolesAsync(userId);
        var permissions = await userManagerService.GetPermissionsAsync(userId);

        var accessToken = jwtTokenService.GenerateAccessToken(userId, externalUserInfo.Email, domainUser.FullName, roles, null, permissions);
        var refreshToken = jwtTokenService.GenerateRefreshToken();
        var refreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        var authResponse = new AuthenticationResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            RefreshTokenExpiryTime = refreshTokenExpiryTime
        };
        await cacheService.SetAsync(externalUserInfo.Email, authResponse, TimeSpan.FromDays(7), cancellationToken);

        // 5. Trả về RegisterResponse
        var response = new RegisterResponse(
            UserId: userId,
            FullName: domainUser.FullName,
            Email: domainUser.EmailAddress,
            PhoneNumber: domainUser.PhoneNumber,
            Gender: domainUser.Gender,
            SkillLevel: domainUser.SkillLevel.ToString(),
            ReputationScore: domainUser.ReputationScore,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            RefreshTokenExpiryTime: refreshTokenExpiryTime
        );

        return Result.Success(response);
    }
}
