using System.Text.Json.Nodes;

namespace BTSS.IAR.Models;

public sealed class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
}

public class ApiClientSummaryDto
{
    public int Id { get; set; }
    public int AgencyId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string SourceSystemCode { get; set; } = string.Empty;
    public string[] AllowedScopes { get; set; } = Array.Empty<string>();
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ApiClientDetailDto : ApiClientSummaryDto
{
    public JsonObject? GlobalSettings { get; set; }
    public JsonArray? DeviceSettings { get; set; }
    public JsonArray? DisplayRegistrations { get; set; }
}

public sealed class ApiClientUpsertModel
{
    public int AgencyId { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string SourceSystemCode { get; set; } = "IAR";
    public string AllowedScopesText { get; set; } = "clients.read global-settings.read device-settings.read display.read";

    public string[] GetAllowedScopes() => AllowedScopesText
        .Split(new[] { ' ', ',', ';', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
}

public sealed class ServiceStatusInfo
{
    public bool IsAvailable { get; set; }
    public string Status { get; set; } = "Unknown";
    public string Detail { get; set; } = string.Empty;
}

public sealed class DeviceCommandDto
{
    public string Id { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public DateTimeOffset? AcknowledgedAtUtc { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? LastHeartbeatAtUtc { get; set; }
    public string? ResultMessage { get; set; }
    public JsonObject? Details { get; set; }
}



public sealed class IncidentTransitionItem
{
    public long Id { get; set; }
    public string IncidentId { get; set; } = string.Empty;
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public bool FromClosed { get; set; }
    public bool ToClosed { get; set; }
    public DateTimeOffset ObservedAtUtc { get; set; }
    public string? Notes { get; set; }
}

public sealed class IncidentUnitItem
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Agency { get; set; } = string.Empty;
    public string Station { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public DateTimeOffset? TimestampUtc { get; set; }
}

public sealed class IncidentCommentItem
{
    public string Message { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
    public string CreatedAgency { get; set; } = string.Empty;
    public DateTimeOffset? CreatedAtUtc { get; set; }
}

public sealed class IncidentDetailView
{
    public IncidentListItem Snapshot { get; set; } = new();
    public string Priority { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string Coordinates { get; set; } = string.Empty;
    public string CallerSummary { get; set; } = string.Empty;
    public string QuerySeed { get; set; } = string.Empty;
    public List<IncidentTransitionItem> Timeline { get; set; } = new();
    public List<IncidentUnitItem> Units { get; set; } = new();
    public List<IncidentCommentItem> Comments { get; set; } = new();
}


