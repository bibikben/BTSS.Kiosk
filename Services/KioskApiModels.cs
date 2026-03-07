using System.Text.Json.Nodes;

namespace BTSS.IAR.Kiosk.Services;

public sealed class AuditMetadataDto
{
    public DateTime? ChangedAtUtc { get; set; }
    public string? ChangedBy { get; set; }
    public string? Reason { get; set; }
    public string? Source { get; set; }
}

public sealed class DeviceProfileDto
{
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? StationCode { get; set; }
    public string? StationName { get; set; }
    public string? StartupUrl { get; set; }
    public string? DisplaySource { get; set; }
    public int? RefreshSeconds { get; set; }
    public string? PrinterRouting { get; set; }
    public string? DefaultPrinterName { get; set; }
    public string? CommandState { get; set; }
    public bool Enabled { get; set; } = true;
    public JsonObject? Metadata { get; set; }
}

public sealed class DeviceResolvedConfigurationDto
{
    public string ClientId { get; set; } = "";
    public int ApiClientId { get; set; }
    public int AgencyId { get; set; }
    public string DeviceId { get; set; } = "";
    public DeviceProfileDto? Profile { get; set; }
    public JsonObject? GlobalSettings { get; set; }
    public JsonObject? DeviceSettings { get; set; }
    public AuditMetadataDto? GlobalSettingsAudit { get; set; }
    public AuditMetadataDto? DeviceAudit { get; set; }
    public AuditMetadataDto? DisplayAudit { get; set; }
    public DateTime? DeviceUpdatedAtUtc { get; set; }
    public DateTime? DisplayUpdatedAtUtc { get; set; }

    public string? ResolveStartupUrl()
    {
        var fromProfile = Profile?.StartupUrl;
        if (!string.IsNullOrWhiteSpace(fromProfile)) return fromProfile;

        var fromDevice = ReadString(DeviceSettings, "startupUrl")
            ?? ReadString(DeviceSettings, "displaySource")
            ?? ReadString(DeviceSettings, "url");
        if (!string.IsNullOrWhiteSpace(fromDevice)) return fromDevice;

        var fromGlobal = ReadString(GlobalSettings, "startupUrl")
            ?? ReadString(GlobalSettings, "displaySource")
            ?? ReadString(GlobalSettings, "url");
        return string.IsNullOrWhiteSpace(fromGlobal) ? null : fromGlobal;
    }

    public int? ResolveMonitorIndex()
    {
        var device = ReadInt(DeviceSettings, "selectedMonitorIndex") ?? ReadInt(DeviceSettings, "monitorIndex");
        if (device.HasValue) return device.Value;

        var global = ReadInt(GlobalSettings, "selectedMonitorIndex") ?? ReadInt(GlobalSettings, "monitorIndex");
        return global;
    }

    private static string? ReadString(JsonObject? obj, string key)
    {
        if (obj is null || !obj.TryGetPropertyValue(key, out var value) || value is null)
            return null;
        return value.GetValue<string?>();
    }

    private static int? ReadInt(JsonObject? obj, string key)
    {
        if (obj is null || !obj.TryGetPropertyValue(key, out var value) || value is null)
            return null;

        try
        {
            return value.GetValue<int>();
        }
        catch
        {
            if (int.TryParse(value.ToString(), out var parsed))
                return parsed;
            return null;
        }
    }
}

public sealed class DeviceBootstrapRequestDto
{
    public string DeviceId { get; set; } = "";
    public DeviceProfileDto? Profile { get; set; }
    public JsonObject? Settings { get; set; }
    public bool UpsertRegistration { get; set; } = true;
    public AuditMetadataDto? Audit { get; set; }
}

public sealed class DeviceRegistrationResponseDto
{
    public string? Message { get; set; }
    public DeviceResolvedConfigurationDto? Configuration { get; set; }
}
