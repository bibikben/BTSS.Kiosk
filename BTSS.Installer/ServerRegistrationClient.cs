using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BTSS.Installer;

internal sealed class ServerRegistrationClient
{
    private readonly HttpClient _httpClient = new();

    public async Task RegisterDeviceAsync(InstallPlan plan, IReadOnlyList<DisplayInfoModel> displays, CancellationToken cancellationToken)
    {
        var token = await RequestTokenAsync(plan, cancellationToken);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.BaseAddress = new Uri(plan.ApiBaseUrl.TrimEnd('/') + "/");

        var settings = new JsonObject
        {
            ["selectedMonitorIndex"] = plan.SelectedMonitorIndex,
            ["startupUrl"] = plan.StartupUrl,
            ["displaySource"] = plan.StartupUrl,
            ["machineName"] = plan.MachineName,
            ["machinePermanentId"] = plan.MachinePermanentId,
            ["detectedDisplays"] = new JsonArray(displays.Select(d => JsonSerializer.SerializeToNode(new
            {
                d.Index,
                d.DeviceName,
                d.Resolution,
                d.Primary,
                d.Bounds
            })!).ToArray())
        };

        var body = new
        {
            deviceId = plan.DeviceId,
            profile = new
            {
                displayName = plan.DisplayName,
                location = plan.Location,
                stationCode = plan.StationCode,
                stationName = plan.StationName,
                startupUrl = plan.StartupUrl,
                displaySource = plan.StartupUrl,
                defaultPrinterName = string.IsNullOrWhiteSpace(plan.PrinterName) ? null : plan.PrinterName,
                enabled = true,
                metadata = new
                {
                    machinePermanentId = plan.MachinePermanentId,
                    installedBy = Environment.UserName,
                    installedAtUtc = DateTime.UtcNow,
                    detectedDisplayCount = displays.Count
                }
            },
            settings,
            upsertRegistration = true,
            audit = new
            {
                changedAtUtc = DateTime.UtcNow,
                changedBy = Environment.UserName,
                reason = "BTSS installer device registration",
                source = "btss-installer"
            }
        };

        using var response = await _httpClient.PostAsync(
            "api/me/devices/register",
            new StringContent(JsonSerializer.Serialize(body, JsonDefaults.Options), Encoding.UTF8, "application/json"),
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private async Task<string> RequestTokenAsync(InstallPlan plan, CancellationToken cancellationToken)
    {
        using var client = new HttpClient { BaseAddress = new Uri(plan.ApiBaseUrl.TrimEnd('/') + "/") };
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = plan.ClientId,
            ["client_secret"] = plan.ClientSecret,
            ["grant_type"] = "client_credentials",
            ["scope"] = plan.Scope
        });

        using var response = await client.PostAsync("connect/token", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.GetProperty("access_token").GetString()
               ?? throw new InvalidOperationException("Token response did not contain access_token.");
    }
}
