using System.Globalization;
using System.Net;
using System.Text.Json;
using BTSS.IAR.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BTSS.IAR.Api.Models;

/// <summary>
/// Best-effort mileage calculation.
///
/// If an agency has station coordinates configured, we compute straight-line (haversine) distance.
/// If you later want true routing, plug in a routing engine (OSRM/GraphHopper/Google) here.
/// </summary>
public sealed class RouteMileageService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly RoutingOptions _opt;

    public RouteMileageService(IHttpClientFactory httpFactory, IOptions<RoutingOptions> opt)
    {
        _httpFactory = httpFactory;
        _opt = opt.Value ?? new RoutingOptions();
    }
    public async Task<double?> TryComputeMileageAsync(AppDbContext db, int agencyId, double? callLat, double? callLng, CancellationToken ct = default)
    {
        if (callLat == null || callLng == null) return null;

        var agency = await db.Agencies.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agencyId, ct);
        if (agency?.StationLatitude == null || agency.StationLongitude == null) return null;

        var lat1 = agency.StationLatitude.Value;
        var lon1 = agency.StationLongitude.Value;
        var lat2 = callLat.Value;
        var lon2 = callLng.Value;

        var provider = (_opt.Provider ?? "Haversine").Trim();
        if (provider.Equals("OSRM", StringComparison.OrdinalIgnoreCase))
            return await TryOsrmAsync(lat1, lon1, lat2, lon2, ct);
        if (provider.Equals("GraphHopper", StringComparison.OrdinalIgnoreCase))
            return await TryGraphHopperAsync(lat1, lon1, lat2, lon2, ct);
        if (provider.Equals("Google", StringComparison.OrdinalIgnoreCase))
            return await TryGoogleDirectionsAsync(lat1, lon1, lat2, lon2, ct);

        // Default: straight line
        return HaversineMiles(lat1, lon1, lat2, lon2);
    }

    private async Task<double?> TryOsrmAsync(double fromLat, double fromLon, double toLat, double toLon, CancellationToken ct)
    {
        var baseUrl = _opt.BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;

        // OSRM expects lon,lat;lon,lat
        var url = $"{baseUrl.TrimEnd('/')}/route/v1/driving/" +
                  $"{ToInvariant(fromLon)},{ToInvariant(fromLat)};{ToInvariant(toLon)},{ToInvariant(toLat)}" +
                  "?overview=false";

        var http = _httpFactory.CreateClient("routing");
        using var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0) return null;
        var distMeters = routes[0].GetProperty("distance").GetDouble();
        return MetersToMiles(distMeters);
    }

    private async Task<double?> TryGraphHopperAsync(double fromLat, double fromLon, double toLat, double toLon, CancellationToken ct)
    {
        var key = _opt.ApiKey;
        if (string.IsNullOrWhiteSpace(key)) return null;

        var baseUrl = string.IsNullOrWhiteSpace(_opt.BaseUrl)
            ? "https://graphhopper.com/api/1"
            : _opt.BaseUrl!.TrimEnd('/');

        // GraphHopper: point=lat,lon
        var url = $"{baseUrl}/route?" +
                  $"point={ToInvariant(fromLat)},{ToInvariant(fromLon)}&" +
                  $"point={ToInvariant(toLat)},{ToInvariant(toLon)}&" +
                  "vehicle=car&calc_points=false&instructions=false&" +
                  $"key={WebUtility.UrlEncode(key)}";

        var http = _httpFactory.CreateClient("routing");
        using var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("paths", out var paths) || paths.GetArrayLength() == 0) return null;

        var distMeters = paths[0].GetProperty("distance").GetDouble();
        return MetersToMiles(distMeters);
    }

    private async Task<double?> TryGoogleDirectionsAsync(double fromLat, double fromLon, double toLat, double toLon, CancellationToken ct)
    {
        var key = _opt.ApiKey;
        if (string.IsNullOrWhiteSpace(key)) return null;

        // Default to Directions API (simplest to wire up).
        var baseUrl = string.IsNullOrWhiteSpace(_opt.BaseUrl)
            ? "https://maps.googleapis.com/maps/api/directions/json"
            : _opt.BaseUrl!;

        var url = baseUrl;
        if (!url.Contains("?")) url += "?";
        if (!url.EndsWith("?") && !url.EndsWith("&")) url += "&";

        url += $"origin={ToInvariant(fromLat)},{ToInvariant(fromLon)}&" +
               $"destination={ToInvariant(toLat)},{ToInvariant(toLon)}&" +
               "mode=driving&" +
               $"key={WebUtility.UrlEncode(key)}";

        var http = _httpFactory.CreateClient("routing");
        using var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0) return null;
        var legs = routes[0].GetProperty("legs");
        if (legs.GetArrayLength() == 0) return null;
        var distMeters = legs[0].GetProperty("distance").GetProperty("value").GetDouble();
        return MetersToMiles(distMeters);
    }

    private static double MetersToMiles(double meters) => meters / 1609.344;

    private static string ToInvariant(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);

    private static double HaversineMiles(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 3958.7613; // Earth radius in miles
        double dLat = DegreesToRadians(lat2 - lat1);
        double dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double DegreesToRadians(double deg) => deg * (Math.PI / 180.0);
}
