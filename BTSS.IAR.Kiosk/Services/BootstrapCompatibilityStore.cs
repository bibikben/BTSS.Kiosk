using System.Text.Json;

namespace BTSS.IAR.Kiosk.Services;

public static class BootstrapCompatibilityStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static string FilePath => Path.Combine(FileSystem.Current.AppDataDirectory, "bootstrap-compat.json");

    public static void LoadIfNeeded()
    {
        if (!File.Exists(FilePath))
            return;

        if (!string.IsNullOrWhiteSpace(AppSettings.IarApiClientId) || !string.IsNullOrWhiteSpace(AppSettings.IarApiClientSecret))
            return;

        try
        {
            var payload = JsonSerializer.Deserialize<BootstrapCompatibilitySnapshot>(File.ReadAllText(FilePath), JsonOptions);
            if (payload is null)
                return;

            AppSettings.IarApiBaseUrl = payload.ApiBaseUrl ?? AppSettings.IarApiBaseUrl;
            AppSettings.IarApiClientId = payload.ApiClientId ?? AppSettings.IarApiClientId;
            AppSettings.IarApiClientSecret = payload.ApiClientSecret ?? AppSettings.IarApiClientSecret;
            AppSettings.KioskDisplayName = payload.DisplayName ?? AppSettings.KioskDisplayName;
            AppSettings.KioskLocation = payload.Location ?? AppSettings.KioskLocation;
            AppSettings.KioskStationCode = payload.StationCode ?? AppSettings.KioskStationCode;
            AppSettings.KioskStationName = payload.StationName ?? AppSettings.KioskStationName;
            AppSettings.SavedUrl = payload.StartupUrl ?? AppSettings.SavedUrl;
            if (payload.SelectedMonitorIndex.HasValue)
                AppSettings.SelectedMonitorIndex = payload.SelectedMonitorIndex.Value;
        }
        catch
        {
        }
    }

    public static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var payload = new BootstrapCompatibilitySnapshot
        {
            ApiBaseUrl = AppSettings.IarApiBaseUrl,
            ApiClientId = AppSettings.IarApiClientId,
            ApiClientSecret = AppSettings.IarApiClientSecret,
            DisplayName = AppSettings.KioskDisplayName,
            Location = AppSettings.KioskLocation,
            StationCode = AppSettings.KioskStationCode,
            StationName = AppSettings.KioskStationName,
            StartupUrl = AppSettings.SavedUrl,
            SelectedMonitorIndex = AppSettings.SelectedMonitorIndex
        };

        File.WriteAllText(FilePath, JsonSerializer.Serialize(payload, JsonOptions));
    }

    public static void Clear()
    {
        if (File.Exists(FilePath))
            File.Delete(FilePath);
    }
}

public sealed class BootstrapCompatibilitySnapshot
{
    public string? ApiBaseUrl { get; set; }
    public string? ApiClientId { get; set; }
    public string? ApiClientSecret { get; set; }
    public string? DisplayName { get; set; }
    public string? Location { get; set; }
    public string? StationCode { get; set; }
    public string? StationName { get; set; }
    public string? StartupUrl { get; set; }
    public int? SelectedMonitorIndex { get; set; }
}
