using System.Text.Json.Nodes;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BTSS.IAR.Record.Models;
using BTSS.Service.Options;
using Microsoft.Extensions.Options;
using BTSS.Service.Models;

namespace BTSS.Service.Services;

public sealed class DeviceSyncClient(
    HttpClient httpClient,
    OAuthTokenClient tokenClient,
    IOptions<ServiceRuntimeOptions> options,
    ILogger<DeviceSyncClient> logger)
{
    private readonly ServiceRuntimeOptions _options = options.Value;

    public async Task<bool> RegisterDeviceAsync(CancellationToken cancellationToken)
    {
        var payload = new
        {
            deviceId = _options.ResolveDeviceId(),
            profile = new
            {
                displayName = _options.DeviceName,
                metadata = new Dictionary<string, string?>
                {
                    ["machineName"] = _options.MachineName,
                    ["deviceType"] = _options.DeviceType
                }
            },
            settings = new Dictionary<string, string?>
            {
                ["localDataDirectory"] = _options.LocalDataDirectory,
                ["printerName"] = _options.PrinterName
            },
            upsertRegistration = true
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.DeviceRegisterPath)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        var auth = await tokenClient.CreateHeaderAsync(cancellationToken);
        if (auth is not null) request.Headers.Authorization = auth;
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            logger.LogWarning("Device registration failed with HTTP {StatusCode}", (int)response.StatusCode);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> SendHeartbeatAsync(string? status, string? message, CancellationToken cancellationToken)
    {
        var payload = new DeviceHeartbeatPayload
        {
            DeviceId = _options.ResolveDeviceId(),
            Status = status,
            Message = message,
            Payload = new JsonObject
            {
                ["machineName"] = _options.MachineName,
                ["deviceType"] = _options.DeviceType,
                ["pollIntervalSeconds"] = _options.PollIntervalSeconds
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.DeviceHeartbeatPath)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        var auth = await tokenClient.CreateHeaderAsync(cancellationToken);
        if (auth is not null) request.Headers.Authorization = auth;
        using var response = await httpClient.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<SyncBootstrapDocument?> BootstrapAsync(DateTimeOffset? sinceUtc, CancellationToken cancellationToken)
    {
        var uri = $"{_options.SyncBootstrapPath}?deviceId={Uri.EscapeDataString(_options.ResolveDeviceId())}";
        if (sinceUtc.HasValue)
            uri += $"&sinceUtc={Uri.EscapeDataString(sinceUtc.Value.UtcDateTime.ToString("O"))}";

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var auth = await tokenClient.CreateHeaderAsync(cancellationToken);
        if (auth is not null) request.Headers.Authorization = auth;
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<SyncBootstrapDocument>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });
    }

    public async Task<SyncChangesDocument?> GetChangesAsync(DateTimeOffset? sinceUtc, CancellationToken cancellationToken)
    {
        var uri = $"{_options.SyncChangesPath}?deviceId={Uri.EscapeDataString(_options.ResolveDeviceId())}";
        if (sinceUtc.HasValue)
            uri += $"&sinceUtc={Uri.EscapeDataString(sinceUtc.Value.UtcDateTime.ToString("O"))}";

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        var auth = await tokenClient.CreateHeaderAsync(cancellationToken);
        if (auth is not null) request.Headers.Authorization = auth;
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<SyncChangesDocument>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });
    }

    public async Task AckAsync(SyncAckEnvelope payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.SyncAckPath)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        var auth = await tokenClient.CreateHeaderAsync(cancellationToken);
        if (auth is not null) request.Headers.Authorization = auth;
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}

public sealed class SyncBootstrapDocument
{
    public string BatchId { get; set; } = string.Empty;
    public JsonElement[] Incidents { get; set; } = [];
    public JsonElement[] Comments { get; set; } = [];
    public JsonElement[] Units { get; set; } = [];
    public JsonElement[] Timeline { get; set; } = [];
    public DateTime ServerUtc { get; set; }
}

public sealed class SyncChangesDocument
{
    public string BatchId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public DateTime ServerUtc { get; set; }
    public SyncChangeItem[] Changes { get; set; } = [];
}

public sealed class SyncChangeItem
{
    public long SyncLogId { get; set; }
    public JsonElement Incident { get; set; }
    public JsonElement[] Comments { get; set; } = [];
    public JsonElement[] Units { get; set; } = [];
    public JsonElement[] Timeline { get; set; } = [];
}
