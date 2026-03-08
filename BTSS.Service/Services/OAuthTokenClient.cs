using System.Net.Http.Headers;
using System.Text.Json;
using BTSS.Service.Options;
using Microsoft.Extensions.Options;

namespace BTSS.Service.Services;

public sealed class OAuthTokenClient(HttpClient httpClient, IOptions<ServiceRuntimeOptions> options, ILogger<OAuthTokenClient> logger)
{
    private readonly ServiceRuntimeOptions _options = options.Value;
    private string? _token;
    private DateTimeOffset _expiresAtUtc = DateTimeOffset.MinValue;

    public async Task<AuthenticationHeaderValue?> CreateHeaderAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ClientSecret) ||
            string.IsNullOrWhiteSpace(_options.OAuthTokenPath))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_token) && _expiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
            return new AuthenticationHeaderValue("Bearer", _token);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.OAuthTokenPath)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = _options.Scope
            })
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError(
                "OAuth token request failed. Status={StatusCode}, ClientId={ClientId}, Scope={Scope}, TokenPath={TokenPath}, Response={ResponseBody}",
                (int)response.StatusCode,
                _options.ClientId,
                _options.Scope,
                _options.OAuthTokenPath,
                body);

            throw new HttpRequestException(
                $"OAuth token request failed with status {(int)response.StatusCode} ({response.StatusCode}). Response: {body}");
        }

        using var document = JsonDocument.Parse(body);
        _token = document.RootElement.GetProperty("access_token").GetString();

        if (string.IsNullOrWhiteSpace(_token))
            throw new InvalidOperationException("OAuth token response did not contain access_token.");

        var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expiresEl)
            ? expiresEl.GetInt32()
            : 3600;

        _expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

        logger.LogInformation("Acquired service access token expiring at {ExpiresAtUtc}.", _expiresAtUtc);
        return new AuthenticationHeaderValue("Bearer", _token);
    }
}