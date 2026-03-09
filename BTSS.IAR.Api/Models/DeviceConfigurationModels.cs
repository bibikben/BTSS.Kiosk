using System.Text.Json.Nodes;

namespace BTSS.IAR.Api.Models;

public sealed class AuditMetadata
{
    public DateTime ChangedAtUtc { get; set; }
    public string? ChangedBy { get; set; }
    public string? Reason { get; set; }
    public string? Source { get; set; }
}

public sealed class GlobalSettingsDocument
{
    public JsonObject Settings { get; set; } = new();
    public AuditMetadata? Audit { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class DeviceProfile
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
    public JsonObject Metadata { get; set; } = new();
}

public sealed class DeviceSettingsEntry
{
    public string DeviceId { get; set; } = "";
    public DeviceProfile? Profile { get; set; }
    public JsonObject Settings { get; set; } = new();
    public AuditMetadata? Audit { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class DisplayRegistrationEntry
{
    public string DeviceId { get; set; } = "";
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public bool Enabled { get; set; } = true;
    public JsonObject Settings { get; set; } = new();
    public DeviceProfile? Profile { get; set; }
    public AuditMetadata? Audit { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class DeviceResolvedConfiguration
{
    public string ClientId { get; set; } = "";
    public int ApiClientId { get; set; }
    public int AgencyId { get; set; }
    public string DeviceId { get; set; } = "";
    public DeviceProfile? Profile { get; set; }
    public JsonObject GlobalSettings { get; set; } = new();
    public JsonObject DeviceSettings { get; set; } = new();
    public AuditMetadata? GlobalSettingsAudit { get; set; }
    public AuditMetadata? DeviceAudit { get; set; }
    public AuditMetadata? DisplayAudit { get; set; }
    public DateTime? DeviceUpdatedAtUtc { get; set; }
    public DateTime? DisplayUpdatedAtUtc { get; set; }
}

public sealed class DeviceCommandEntry
{
    public string Id { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string Command { get; set; } = "";
    public string Status { get; set; } = DeviceCommandStatuses.Pending;
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? LastHeartbeatAtUtc { get; set; }
    public string? ResultMessage { get; set; }
    public AuditMetadata? Audit { get; set; }
    public JsonObject Details { get; set; } = new();
}

public static class DeviceCommandStatuses
{
    public const string Pending = "pending";
    public const string Acknowledged = "acknowledged";
    public const string Running = "running";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Stale = "stale";
}
