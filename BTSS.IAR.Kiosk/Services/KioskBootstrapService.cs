using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BTSS.IAR.Kiosk.Services.IarApi;
using BTSS.IAR.Record.Models;

namespace BTSS.IAR.Kiosk.Services;

public interface IKioskBootstrapService
{
    DeviceBootstrapInfo BuildLocalBootstrapInfo();
    string GetDeviceId();
    Task<DeviceResolvedConfigurationDto?> TryGetConfigurationAsync(CancellationToken ct = default);
    Task<DeviceResolvedConfigurationDto?> RegisterDeviceAsync(DeviceProfileDto profile, int selectedMonitorIndex, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceCommandDto>> GetPendingCommandsAsync(string deviceId, CancellationToken ct = default);
    Task<DeviceCommandDto?> AcknowledgeCommandAsync(string deviceId, string commandId, string status, string? message = null, JsonObject? details = null, CancellationToken ct = default);
    Task<DeviceCommandDto?> SendHeartbeatAsync(string deviceId, string commandId, string? status = null, string? message = null, JsonObject? details = null, CancellationToken ct = default);
    void ClearSavedApiSettings();
}

internal sealed class KioskBootstrapService : IKioskBootstrapService
{
    private readonly IarTokenProvider _tokenProvider;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public KioskBootstrapService(IarTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    public DeviceBootstrapInfo BuildLocalBootstrapInfo()
    {
        return new DeviceBootstrapInfo(
            DeviceId: GetDeviceId(),
            MachineName: Environment.MachineName,
            OsUser: Environment.UserName,
            ApplicationName: AppInfo.Current.Name,
            ApplicationVersion: AppInfo.Current.VersionString,
            StartupUrl: AppSettings.SavedUrl,
            SelectedMonitorIndex: AppSettings.SelectedMonitorIndex,
            StationName: AppSettings.KioskStationName,
            LocationName: AppSettings.KioskLocation,
            IsPaired: !string.IsNullOrWhiteSpace(AppSettings.IarApiClientId),
            CapturedAtUtc: DateTimeOffset.UtcNow);
    }

    public async Task<DeviceResolvedConfigurationDto?> TryGetConfigurationAsync(CancellationToken ct = default)
    {
        var deviceId = GetDeviceId();
        using var client = await CreateAuthorizedClientAsync(ct);
        using var resp = await client.GetAsync($"api/me/devices/{Uri.EscapeDataString(deviceId)}/configuration", ct);

        if (resp.StatusCode == HttpStatusCode.NotFound)
            return null;

        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<DeviceResolvedConfigurationDto>(json, _json);
    }

    public async Task<DeviceResolvedConfigurationDto?> RegisterDeviceAsync(DeviceProfileDto profile, int selectedMonitorIndex, CancellationToken ct = default)
    {
        var deviceId = GetDeviceId();
        using var client = await CreateAuthorizedClientAsync(ct);

        var req = new DeviceBootstrapRequestDto
        {
            DeviceId = deviceId,
            Profile = profile,
            Settings = new JsonObject
            {
                ["selectedMonitorIndex"] = selectedMonitorIndex,
                ["startupUrl"] = profile.StartupUrl ?? string.Empty,
                ["displaySource"] = profile.DisplaySource ?? string.Empty,
                ["defaultPrinterName"] = profile.DefaultPrinterName ?? string.Empty,
                ["exportFolder"] = profile.Metadata is not null && profile.Metadata.TryGetPropertyValue("exportFolder", out var exportFolder) ? exportFolder?.GetValue<string?>() ?? string.Empty : string.Empty,
                ["templateFolder"] = profile.Metadata is not null && profile.Metadata.TryGetPropertyValue("templateFolder", out var templateFolder) ? templateFolder?.GetValue<string?>() ?? string.Empty : string.Empty
            },
            UpsertRegistration = true,
            Audit = new AuditMetadataDto
            {
                ChangedAtUtc = DateTime.UtcNow,
                ChangedBy = Environment.UserName,
                Reason = "Kiosk first-run bootstrap",
                Source = "kiosk-bootstrap"
            }
        };

        using var resp = await client.PostAsync(
            "api/me/devices/register",
            new StringContent(JsonSerializer.Serialize(req, _json), Encoding.UTF8, "application/json"),
            ct);

        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        var dto = JsonSerializer.Deserialize<DeviceRegistrationResponseDto>(json, _json);
        return dto?.Configuration;
    }


    public async Task<IReadOnlyList<DeviceCommandDto>> GetPendingCommandsAsync(string deviceId, CancellationToken ct = default)
    {
        using var client = await CreateAuthorizedClientAsync(ct);
        using var resp = await client.GetAsync($"api/me/devices/{Uri.EscapeDataString(deviceId)}/commands/pending", ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<DeviceCommandDto>>(json, _json) ?? new();
    }

    public async Task<DeviceCommandDto?> AcknowledgeCommandAsync(string deviceId, string commandId, string status, string? message = null, JsonObject? details = null, CancellationToken ct = default)
    {
        using var client = await CreateAuthorizedClientAsync(ct);
        var payload = new DeviceCommandAckRequestDto { Status = status, Message = message, Details = details };
        using var resp = await client.PostAsync($"api/me/devices/{Uri.EscapeDataString(deviceId)}/commands/{Uri.EscapeDataString(commandId)}/ack", new StringContent(JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json"), ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<DeviceCommandDto>(json, _json);
    }

    public async Task<DeviceCommandDto?> SendHeartbeatAsync(string deviceId, string commandId, string? status = null, string? message = null, JsonObject? details = null, CancellationToken ct = default)
    {
        using var client = await CreateAuthorizedClientAsync(ct);
        var payload = new DeviceHeartbeatRequestDto { Status = status, Message = message, Details = details };
        using var resp = await client.PostAsync($"api/me/devices/{Uri.EscapeDataString(deviceId)}/commands/{Uri.EscapeDataString(commandId)}/heartbeat", new StringContent(JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json"), ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<DeviceCommandDto>(json, _json);
    }

    public void ClearSavedApiSettings()
    {
        AppSettings.IarApiBaseUrl = "http://localhost:5080";
        AppSettings.IarApiClientId = string.Empty;
        AppSettings.IarApiClientSecret = string.Empty;
    }

    private async Task<HttpClient> CreateAuthorizedClientAsync(CancellationToken ct)
    {
        var baseUrl = (AppSettings.IarApiBaseUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("API base URL is required.");

        var token = await _tokenProvider.GetAccessTokenAsync(ct);
        var client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
        };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public string GetDeviceId() => $"{Environment.MachineName}-{Environment.UserName}";
}
