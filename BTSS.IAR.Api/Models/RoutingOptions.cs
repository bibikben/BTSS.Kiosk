namespace BTSS.IAR.Api.Models;

public sealed class RoutingOptions
{
    /// <summary>
    /// Haversine | OSRM | GraphHopper | Google
    /// </summary>
    public string Provider { get; set; } = "Haversine";

    /// <summary>
    /// For OSRM: base URL like http://localhost:5000 (no trailing slash).
    /// For GraphHopper/Google: optional base override; defaults are used if empty.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// GraphHopper: API key.
    /// Google: API key.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Google: if you want to use the newer Routes API instead of Directions API.
    /// Not implemented by default.
    /// </summary>
    public bool UseGoogleRoutesApi { get; set; } = false;
}
