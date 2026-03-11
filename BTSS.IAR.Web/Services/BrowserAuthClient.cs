using System.Text.Json;
using BTSS.IAR.Api.Auth;
using Microsoft.JSInterop;

namespace BTSS.IAR.Web.Services;

public sealed class BrowserAuthClient
{
    private readonly IJSRuntime _js;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public BrowserAuthClient(IJSRuntime js)
    {
        _js = js;
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync()
    {
        var result = await _js.InvokeAsync<FetchResult>("btssAuth.get", "/auth/me");
        if (!result.ok)
            return null;

        return string.IsNullOrWhiteSpace(result.text)
            ? null
            : JsonSerializer.Deserialize<CurrentUserDto>(result.text, JsonOptions);
    }

    public async Task<LoginResult> LoginAsync(HumanLoginRequest request)
    {
        var result = await _js.InvokeAsync<FetchResult>("btssAuth.post", "/auth/login", request);

        if (!result.ok)
            return new LoginResult(false, result.status, null);

        var user = string.IsNullOrWhiteSpace(result.text)
            ? null
            : JsonSerializer.Deserialize<CurrentUserDto>(result.text, JsonOptions);

        return new LoginResult(true, result.status, user);
    }

    public async Task<CurrentUserDto?> SwitchAgencyAsync(int agencyId)
    {
        var result = await _js.InvokeAsync<FetchResult>(
            "btssAuth.post",
            "/auth/switch-agency",
            new SwitchAgencyRequest(agencyId));

        if (!result.ok)
            return null;

        return string.IsNullOrWhiteSpace(result.text)
            ? null
            : JsonSerializer.Deserialize<CurrentUserDto>(result.text, JsonOptions);
    }

    public async Task<bool> LogoutAsync()
    {
        var result = await _js.InvokeAsync<FetchResult>("btssAuth.post", "/auth/logout", new { });
        return result.ok;
    }

    public sealed class FetchResult
    {
        public bool ok { get; set; }
        public int status { get; set; }
        public string text { get; set; } = string.Empty;
    }

    public sealed record LoginResult(bool Success, int StatusCode, CurrentUserDto? User);
}