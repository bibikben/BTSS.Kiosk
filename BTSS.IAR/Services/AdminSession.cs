using BTSS.IAR.Models;

namespace BTSS.IAR.Services;

public sealed class AdminSession
{
    public AdminPreferences Preferences { get; set; } = new();
    public string? AccessToken { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public ApiClientDetailDto? Me { get; set; }

    public bool HasValidToken => !string.IsNullOrWhiteSpace(AccessToken) && ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1);
}
