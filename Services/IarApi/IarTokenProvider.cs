using System.Text;
using System.Text.Json;
using BTSS.IAR.Kiosk.Services;

namespace BTSS.IAR.Kiosk.Services.IarApi;

internal sealed class IarTokenProvider
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;
    private DateTime _expiresAt;

    public IarTokenProvider(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        // Simple cache
        if (!string.IsNullOrWhiteSpace(_token) && DateTime.UtcNow < _expiresAt.AddSeconds(-30))
            return _token;

        await _gate.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(_token) && DateTime.UtcNow < _expiresAt.AddSeconds(-30))
                return _token;

            var clientId = AppSettings.IarApiClientId;
            var clientSecret = AppSettings.IarApiClientSecret;
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                throw new InvalidOperationException("IAR API ClientId/ClientSecret not set.");

            var req = new
            {
                clientId,
                clientSecret,
                grantType = "client_credentials"
            };

            using var resp = await _http.PostAsync("connect/token",
                new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json"), ct);
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync(ct);

            using var doc = JsonDocument.Parse(json);
            _token = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;
            _expiresAt = DateTime.UtcNow.AddSeconds(expiresIn);

            return _token ?? throw new InvalidOperationException("Token response missing access_token.");
        }
        finally
        {
            _gate.Release();
        }
    }
}
