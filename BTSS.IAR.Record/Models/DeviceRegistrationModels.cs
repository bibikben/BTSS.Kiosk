namespace BTSS.IAR.Record.Models;

/// <summary>
/// Shared device bootstrap and registration contracts used by the API, kiosk, service, and admin app.
/// Epic 1 keeps these models centralized even before the full API/device workflow is implemented.
/// </summary>
public sealed record DeviceBootstrapInfo(
    string DeviceId,
    string MachineName,
    string OsUser,
    string ApplicationName,
    string ApplicationVersion,
    string? StartupUrl,
    int? SelectedMonitorIndex,
    string? StationName,
    string? LocationName,
    bool IsPaired,
    DateTimeOffset CapturedAtUtc);

public sealed record DeviceAssignment(
    string ApiClientId,
    string DeviceId,
    string DisplayName,
    string? StartupUrl,
    string? StationName,
    string? LocationName,
    bool IsEnabled,
    DateTimeOffset UpdatedAtUtc);

public sealed record ApplicationSettingsEnvelope(
    string ApiClientId,
    IReadOnlyDictionary<string, string>? GlobalSettings,
    IReadOnlyDictionary<string, string>? DeviceSettings,
    DateTimeOffset UpdatedAtUtc);
