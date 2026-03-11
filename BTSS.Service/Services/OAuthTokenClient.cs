using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;
using BTSS.Service.Options;
using Microsoft.Extensions.Options;

namespace BTSS.Service.Services;

public sealed class OAuthTokenClient(HttpClient httpClient, IOptions<ServiceRuntimeOptions> options, ILogger<OAuthTokenClient> logger)
{
    private readonly ServiceRuntimeOptions _options = options.Value;
    private readonly ConcurrentDictionary<string, CachedToken> _cache = new(StringComparer.Ordinal);

    public Task<AuthenticationHeaderValue?> CreateHeaderAsync(CancellationToken cancellationToken)
        => CreateHeaderAsync(cancellationToken, null);

    public async Task<AuthenticationHeaderValue?> CreateHeaderAsync(CancellationToken cancellationToken, string? scopeOverride)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId) ||
            string.IsNullOrWhiteSpace(_options.ClientSecret) ||
            string.IsNullOrWhiteSpace(_options.OAuthTokenPath))
        {
            return null;
        }

        var effectiveScope = string.IsNullOrWhiteSpace(scopeOverride) ? _options.Scope : scopeOverride.Trim();
        if (_cache.TryGetValue(effectiveScope, out var cached) && cached.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
            return new AuthenticationHeaderValue("Bearer", cached.Token);

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.OAuthTokenPath)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = effectiveScope
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
                effectiveScope,
                _options.OAuthTokenPath,
                body);

            throw new HttpRequestException(
                $"OAuth token request failed with status {(int)response.StatusCode} ({response.StatusCode}). Response: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var token = document.RootElement.GetProperty("access_token").GetString();

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("OAuth token response did not contain access_token.");

        var expiresIn = document.RootElement.TryGetProperty("expires_in", out var expiresEl)
            ? expiresEl.GetInt32()
            : 3600;

        var expiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        _cache[effectiveScope] = new CachedToken(token, expiresAtUtc);

        logger.LogInformation("Acquired service access token for scope {Scope} expiring at {ExpiresAtUtc}.", effectiveScope, expiresAtUtc);
        return new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed record CachedToken(string Token, DateTimeOffset ExpiresAtUtc);
}
