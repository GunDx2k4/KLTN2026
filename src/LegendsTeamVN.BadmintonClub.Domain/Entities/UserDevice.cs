using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class UserDevice : Entity<Guid>
{
    public Guid UserId { get; private set; }
    public string DeviceToken { get; private set; } = default!;

    /// <summary>
    /// Nền tảng thiết bị của thiết bị người dùng. Miền giá trị: ANDROID, IOS. Cho phép NULL.
    /// </summary>
    public DevicePlatform? Platform { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public virtual AppUser User { get; private set; } = default!;

    protected UserDevice() { }

    public UserDevice(Guid userId, string deviceToken, DevicePlatform? platform = null, bool isActive = true)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        DeviceToken = deviceToken;
        Platform = platform;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    public UserDevice(Guid userId, string deviceToken, string? platform, bool isActive = true)
        : this(userId, deviceToken, ParsePlatform(platform), isActive)
    {
    }

    public void UpdateToken(string deviceToken, DevicePlatform? platform)
    {
        DeviceToken = deviceToken;
        Platform = platform;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateToken(string deviceToken, string? platform)
    {
        UpdateToken(deviceToken, ParsePlatform(platform));
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    private static DevicePlatform? ParsePlatform(string? platform)
    {
        if (string.IsNullOrWhiteSpace(platform))
            return null;

        return Enum.TryParse<DevicePlatform>(platform, true, out var result) ? result : null;
    }
}
