using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BTSS.IAR.Models;

namespace BTSS.IAR.Services;

public sealed class AdminApiClient(HttpClient httpClient)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public async Task<TokenResponse> RequestTokenAsync(AdminPreferences preferences, CancellationToken cancellationToken = default)
    {
        var baseUrl = NormalizeBaseUrl(preferences.ApiBaseUrl);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(baseUrl), "/connect/token"));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = preferences.ClientId,
            ["client_secret"] = preferences.ClientSecret,
            ["scope"] = preferences.Scope
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Token request failed with status {(int)response.StatusCode} ({response.StatusCode}). Response: {body}");
        }

        var node = JsonNode.Parse(body)?.AsObject() ?? new JsonObject();
        return new TokenResponse
        {
            AccessToken = node["access_token"]?.GetValue<string>() ?? string.Empty,
            TokenType = node["token_type"]?.GetValue<string>() ?? "Bearer",
            ExpiresIn = node["expires_in"]?.GetValue<int?>() ?? 3600
        };
    }

    public async Task<ApiClientDetailDto?> GetMeAsync(AdminSession session, CancellationToken cancellationToken = default)
        => await SendAsync<ApiClientDetailDto>(session, HttpMethod.Get, "/api/me", null, cancellationToken);

    public async Task<List<ApiClientSummaryDto>> GetClientsAsync(AdminSession session, CancellationToken cancellationToken = default)
        => await SendAsync<List<ApiClientSummaryDto>>(session, HttpMethod.Get, "/api/clients", null, cancellationToken) ?? new();

    public async Task<JsonObject?> GetMyGlobalSettingsAsync(AdminSession session, CancellationToken cancellationToken = default)
        => await SendAsync<JsonObject>(session, HttpMethod.Get, "/api/me/global-settings", null, cancellationToken);

    public async Task<List<JsonObject>> GetMyDevicesAsync(AdminSession session, CancellationToken cancellationToken = default)
        => await SendAsync<List<JsonObject>>(session, HttpMethod.Get, "/api/me/devices", null, cancellationToken) ?? new();

    public async Task<JsonObject?> GetClientGlobalSettingsAsync(AdminSession session, int clientId, CancellationToken cancellationToken = default)
        => await SendAsync<JsonObject>(session, HttpMethod.Get, $"/api/clients/{clientId}/global-settings", null, cancellationToken);

    public async Task<List<JsonObject>> GetClientDevicesAsync(AdminSession session, int clientId, CancellationToken cancellationToken = default)
        => await SendAsync<List<JsonObject>>(session, HttpMethod.Get, $"/api/clients/{clientId}/device-settings", null, cancellationToken) ?? new();

    public async Task<List<JsonObject>> GetClientDisplaysAsync(AdminSession session, int clientId, CancellationToken cancellationToken = default)
        => await SendAsync<List<JsonObject>>(session, HttpMethod.Get, $"/api/clients/{clientId}/displays", null, cancellationToken) ?? new();

    public async Task<JsonObject?> SaveClientGlobalSettingsAsync(AdminSession session, int clientId, JsonObject settings, CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject
        {
            ["settings"] = settings,
            ["audit"] = new JsonObject
            {
                ["actor"] = "BTSS.IAR",
                ["reason"] = "admin-shell-save"
            }
        };

        return await SendAsync<JsonObject>(session, HttpMethod.Put, $"/api/clients/{clientId}/global-settings", payload, cancellationToken);
    }

    public async Task<JsonObject?> SaveDeviceSettingsAsync(AdminSession session, int clientId, string deviceId, JsonObject profile, JsonObject settings, CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject
        {
            ["deviceId"] = deviceId,
            ["profile"] = profile,
            ["settings"] = settings,
            ["audit"] = new JsonObject
            {
                ["actor"] = "BTSS.IAR",
                ["reason"] = "admin-shell-save-device"
            }
        };

        return await SendAsync<JsonObject>(session, HttpMethod.Put, $"/api/clients/{clientId}/device-settings/{Uri.EscapeDataString(deviceId)}", payload, cancellationToken);
    }

    public async Task<JsonObject?> SaveDisplayAsync(AdminSession session, int clientId, string deviceId, JsonObject payload, bool create, CancellationToken cancellationToken = default)
        => await SendAsync<JsonObject>(session, create ? HttpMethod.Post : HttpMethod.Put, create ? $"/api/clients/{clientId}/displays" : $"/api/clients/{clientId}/displays/{Uri.EscapeDataString(deviceId)}", payload, cancellationToken);

    public async Task<JsonObject?> CreateClientAsync(AdminSession session, ApiClientUpsertModel model, CancellationToken cancellationToken = default)
    {
        var payload = new JsonObject
        {
            ["agencyId"] = model.AgencyId,
            ["clientId"] = model.ClientId,
            ["clientSecret"] = model.ClientSecret,
            ["name"] = model.Name,
            ["isEnabled"] = model.IsEnabled,
            ["sourceSystemCode"] = model.SourceSystemCode,
            ["allowedScopes"] = new JsonArray(model.GetAllowedScopes().Select(scope => (JsonNode?)JsonValue.Create(scope)).ToArray())
        };

        return await SendAsync<JsonObject>(session, HttpMethod.Post, "/api/clients", payload, cancellationToken);
    }

    public async Task<DeviceCommandDto?> SendKioskCommandAsync(AdminSession session, string command, string? deviceId, CancellationToken cancellationToken = default)
    {
        var clientId = session.Preferences.PreferredClientNumericId ?? session.Me?.Id ?? 0;
        if (clientId <= 0)
            throw new InvalidOperationException("Select or configure a client first.");
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new InvalidOperationException("Device id is required.");

        var payload = new JsonObject
        {
            ["deviceId"] = deviceId,
            ["command"] = command,
            ["timeoutSeconds"] = 300,
            ["audit"] = new JsonObject
            {
                ["changedBy"] = session.Me?.ClientId ?? session.Preferences.ClientId,
                ["reason"] = "BTSS.IAR operator command",
                ["source"] = "btss-iar-admin"
            }
        };

        return await SendAsync<DeviceCommandDto>(session, HttpMethod.Post, $"/api/clients/{clientId}/commands", payload, cancellationToken);
    }

    public async Task<List<DeviceCommandDto>> GetDeviceCommandsAsync(AdminSession session, int clientId, string? deviceId, CancellationToken cancellationToken = default)
    {
        var path = $"/api/clients/{clientId}/commands";
        if (!string.IsNullOrWhiteSpace(deviceId))
            path += $"?deviceId={Uri.EscapeDataString(deviceId)}";
        return await SendAsync<List<DeviceCommandDto>>(session, HttpMethod.Get, path, null, cancellationToken) ?? new();
    }

    private async Task<T?> SendAsync<T>(AdminSession session, HttpMethod method, string path, JsonNode? payload, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(session.AccessToken))
            throw new InvalidOperationException("Authenticate first.");

        var baseUrl = NormalizeBaseUrl(session.Preferences.ApiBaseUrl);
        using var request = new HttpRequestMessage(method, new Uri(new Uri(baseUrl), path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        if (payload is not null)
        {
            request.Content = new StringContent(payload.ToJsonString(JsonOptions), Encoding.UTF8, "application/json");
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        if (typeof(T) == typeof(string))
            return (T)(object)body;
        if (string.IsNullOrWhiteSpace(body))
            return default;
        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    private static string NormalizeBaseUrl(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "https://localhost:56800/";
        return value.EndsWith('/') ? value : value + "/";
    }
}
