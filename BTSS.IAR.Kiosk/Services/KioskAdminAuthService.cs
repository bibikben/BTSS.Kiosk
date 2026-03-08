using System.Net;
using System.Net.Http.Json;

namespace BTSS.IAR.Kiosk.Services;

public sealed record KioskHumanLoginRequest(string? UserName, string? Password, int? AgencyId);
public sealed record KioskAgencyMembershipDto(int AgencyId, string AgencyCode, string AgencyName, bool IsDefault);
public sealed record KioskAdminSessionDto(int UserId, string UserName, string DisplayName, bool IsSuperUser, int? ActiveAgencyId, string[] Permissions, KioskAgencyMembershipDto[] Agencies);

public sealed class KioskAdminAuthService
{
    private readonly HttpClient _http = new();
    private KioskAdminSessionDto? _session;

    public KioskAdminSessionDto? CurrentSession => _session;
    public bool HasAdminSession => _session is not null;

    public async Task<KioskAdminSessionDto?> LoginAsync(string baseUrl, string userName, string password, int? agencyId, CancellationToken ct = default)
    {
        var url = baseUrl.TrimEnd('/') + "/auth/device-login";
        var response = await _http.PostAsJsonAsync(url, new KioskHumanLoginRequest(userName, password, agencyId), ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
            return null;

        response.EnsureSuccessStatusCode();
        _session = await response.Content.ReadFromJsonAsync<KioskAdminSessionDto>(cancellationToken: ct);
        return _session;
    }

    public void Logout() => _session = null;
}
