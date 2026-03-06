using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BTSS.IAR.Record.Models;

namespace BTSS.IAR.Kiosk.Services.IarApi;

public interface IIarApiClient
{
    Task<IReadOnlyList<string>> CheckForCloseAsync(int agencyId, CancellationToken ct);
    Task<IarCallRecord?> GetCallRecordAsync(int agencyId, string callIdentifier, CancellationToken ct);
    Task<IReadOnlyList<IarCallListItem>> GetListOfCallsAsync(int agencyId, DateTime? start, DateTime? end, string? callType, CancellationToken ct);

    Task<IReadOnlyList<AgencyDto>> GetAgenciesAsync(CancellationToken ct);
    Task<bool> UpsertAgencyAsync(AgencyDto agency, CancellationToken ct);
}

public record IarCallListItem(
    string CallIdentifier,
    string? Type,
    string? Address,
    bool Closed,
    DateTime? UpdatedAt
);

public record AgencyDto(
    int Id,
    string? Name,
    double? StationLatitude,
    double? StationLongitude
);

internal sealed class IarApiClient : IIarApiClient
{
    private readonly HttpClient _http;
    private readonly IarTokenProvider _tokens;

    public IarApiClient(HttpClient http, IarTokenProvider tokens)
    {
        _http = http;
        _tokens = tokens;
    }

    public async Task<IReadOnlyList<string>> CheckForCloseAsync(int agencyId, CancellationToken ct)
    {
        await AuthorizeAsync(ct);

        var req = new { agencyIdentifier = agencyId };
        using var resp = await _http.PostAsync("api/checkForClose",
            new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json"), ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.GetProperty("callIdentifiers");
        return arr.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList();
    }

    public async Task<IarCallRecord?> GetCallRecordAsync(int agencyId, string callIdentifier, CancellationToken ct)
    {
        await AuthorizeAsync(ct);

        var url = $"api/getCallRecord?agencyId={agencyId}&callIdentifier={Uri.EscapeDataString(callIdentifier)}";
        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<IarCallRecord>(json, JsonOptions);
    }

    public async Task<IReadOnlyList<IarCallListItem>> GetListOfCallsAsync(int agencyId, DateTime? start, DateTime? end, string? callType, CancellationToken ct)
    {
        await AuthorizeAsync(ct);

        var qs = new List<string> { $"agencyId={agencyId}" };
        if (start != null) qs.Add($"startDate={Uri.EscapeDataString(start.Value.ToString("O"))}");
        if (end != null) qs.Add($"endDate={Uri.EscapeDataString(end.Value.ToString("O"))}");
        if (!string.IsNullOrWhiteSpace(callType)) qs.Add($"callType={Uri.EscapeDataString(callType)}");

        var url = "api/getListOfCalls?" + string.Join("&", qs);
        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<IarCallListItem>>(json, JsonOptions) ?? new();
    }

    public async Task<IReadOnlyList<AgencyDto>> GetAgenciesAsync(CancellationToken ct)
    {
        await AuthorizeAsync(ct);
        using var resp = await _http.GetAsync("api/agencies", ct);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<List<AgencyDto>>(json, JsonOptions) ?? new();
    }

    public async Task<bool> UpsertAgencyAsync(AgencyDto agency, CancellationToken ct)
    {
        await AuthorizeAsync(ct);

        // Prefer PUT for updates; if it 404's, POST to create.
        var body = new
        {
            id = agency.Id,
            name = agency.Name,
            stationLatitude = agency.StationLatitude,
            stationLongitude = agency.StationLongitude
        };

        using var put = await _http.PutAsync($"api/agency/{agency.Id}",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);

        if (put.IsSuccessStatusCode) return true;
        if (put.StatusCode != System.Net.HttpStatusCode.NotFound) return false;

        using var post = await _http.PostAsync("api/agency",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);

        return post.IsSuccessStatusCode;
    }
    private async Task AuthorizeAsync(CancellationToken ct)
    {
        var token = await _tokens.GetAccessTokenAsync(ct);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
}
