using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Caching;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Auth.Logout;

public sealed class LogoutCommandHandler(
    ICurrentUserService currentUserService,
    IDeviceRepository deviceRepository,
    ICacheService cacheService) : ICommandHandler<LogoutCommand>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        var email = currentUserService.Email;

        // 1. Hủy kích hoạt thiết bị trong bảng user_device (nếu có deviceToken)
        if (!string.IsNullOrWhiteSpace(request.DeviceToken) && userId.HasValue)
        {
            await deviceRepository.DeactivateDeviceAsync(userId.Value, request.DeviceToken, cancellationToken);
        }

        // 2. Xóa phiên Refresh Token khỏi Cache
        if (!string.IsNullOrEmpty(email))
        {
            await cacheService.RemoveAsync(email, cancellationToken);
        }

        // 3. Đưa AccessToken vào danh sách thu hồi (Token Blacklist) trên Cache
        if (!string.IsNullOrWhiteSpace(request.AccessToken))
        {
            var tokenKey = $"blacklist_token:{request.AccessToken}";
            await cacheService.SetAsync(tokenKey, true, TimeSpan.FromDays(1), cancellationToken);
        }

        return Result.Success();
    }
}
