using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public class DeviceRepository(BadmintonDbContext dbContext) : IDeviceRepository
{
    public async Task<UserDevice?> FindByTokenAsync(string deviceToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
            return null;

        return await dbContext.UserDevices.FirstOrDefaultAsync(d => d.DeviceToken == deviceToken, cancellationToken);
    }

    public async Task UpsertDeviceAsync(Guid userId, string deviceToken, DevicePlatform? platform, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
            return;

        var device = await dbContext.UserDevices.FirstOrDefaultAsync(d => d.DeviceToken == deviceToken, cancellationToken);
        if (device != null)
        {
            device.SetActive(true);
            if (platform.HasValue)
            {
                device.UpdateToken(deviceToken, platform);
            }
            if (device.UserId != userId)
            {
                typeof(UserDevice).GetProperty(nameof(UserDevice.UserId))?.SetValue(device, userId);
            }
        }
        else
        {
            var newDevice = new UserDevice(userId, deviceToken, platform, isActive: true);
            await dbContext.UserDevices.AddAsync(newDevice, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeactivateDeviceAsync(Guid userId, string deviceToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceToken))
            return;

        var device = await dbContext.UserDevices.FirstOrDefaultAsync(
            d => d.DeviceToken == deviceToken && d.UserId == userId, cancellationToken);

        if (device != null)
        {
            device.SetActive(false);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
