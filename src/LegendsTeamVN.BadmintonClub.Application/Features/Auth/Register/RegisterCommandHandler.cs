using LegendsTeamVN.BadmintonClub.Application.DTOs.Auth;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Caching;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.DTOs;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Auth.Register;

public sealed class RegisterCommandHandler(
    IUserRepository userRepository,
    IUserManagerService userManagerService,
    IJwtTokenService jwtTokenService,
    ICacheService cacheService) : ICommandHandler<RegisterCommand, RegisterResponse>
{
    public async Task<Result<RegisterResponse>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        // 1. Kiểm tra tài khoản đã tồn tại trong Domain
        var isExist = await userRepository.IsUserExistAsync(request.PhoneNumber, request.Email, cancellationToken);
        if (isExist)
        {
            return Result.Failure<RegisterResponse>(
                Error.Conflict("User.AlreadyExists", "Tài khoản với số điện thoại hoặc email này đã tồn tại trong hệ thống."));
        }

        // 2. Tạo tài khoản trong Identity (Authentication)
        var userName = !string.IsNullOrWhiteSpace(request.Email) ? request.Email : request.PhoneNumber!;
        var (succeeded, identityUserId, errors) = await userManagerService.CreateUserAsync(
            userName,
            request.Email,
            request.PhoneNumber,
            request.Password
        );

        if (!succeeded || identityUserId == null)
        {
            return Result.Failure<RegisterResponse>(
                Error.Validation("User.RegisterFailed", string.Join(", ", errors)));
        }

        // 3. Khởi tạo Domain AppUser (Nghiệp vụ hồ sơ thể thao)
        var appUser = AppUser.CreateStandard(
            id: identityUserId.Value,
            fullName: request.FullName,
            emailAddress: request.Email,
            phoneNumber: request.PhoneNumber,
            gender: request.Gender,
            skillLevel: request.SkillLevel
        );

        userRepository.Add(appUser);

        // 4. Phát hành JWT AccessToken & RefreshToken từ Identity
        var roles = await userManagerService.GetRolesAsync(identityUserId.Value);
        var permissions = await userManagerService.GetPermissionsAsync(identityUserId.Value);

        var emailOrUser = request.Email ?? userName;
        var accessToken = jwtTokenService.GenerateAccessToken(identityUserId.Value, emailOrUser, userName, roles, null, permissions);
        var refreshToken = jwtTokenService.GenerateRefreshToken();
        var refreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);

        var authResponse = new AuthenticationResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            RefreshTokenExpiryTime = refreshTokenExpiryTime
        };
        await cacheService.SetAsync(emailOrUser, authResponse, TimeSpan.FromDays(7), cancellationToken);

        // 5. Trả về RegisterResponse
        var response = new RegisterResponse(
            UserId: identityUserId.Value,
            FullName: appUser.FullName,
            Email: appUser.EmailAddress,
            PhoneNumber: appUser.PhoneNumber,
            Gender: appUser.Gender,
            SkillLevel: appUser.SkillLevel.ToString(),
            ReputationScore: appUser.ReputationScore,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            RefreshTokenExpiryTime: refreshTokenExpiryTime
        );

        return Result.Success(response);
    }
}
