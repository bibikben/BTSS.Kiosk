using System.Text.Json.Nodes;

namespace BTSS.IAR.Api.Models;

public sealed class ReceiveCallDetailsRequest
{
    public string SystemIdentifier { get; set; } = "IAR";
    public string Payload { get; set; } = "";
    public int AgencyIdentifier { get; set; }
}

public sealed class ReceiveCallDetailsResponse
{
    public string CallIdentifier { get; set; } = "";
    public double? EstimatedMilesFromStation { get; set; }
    public bool Persisted { get; set; }
    public string Message { get; set; } = "";
}

public sealed class CheckForCloseRequest
{
    public int AgencyIdentifier { get; set; }
}

public sealed class TokenRequest
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string GrantType { get; set; } = "client_credentials";
    public string Scope { get; set; } = "";
}

public sealed class SeedClientRequest
{
    public int AgencyId { get; set; }
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string? Name { get; set; }
    public string? SourceSystemCode { get; set; }
    public JsonObject? GlobalSettings { get; set; }
    public string[]? AllowedScopes { get; set; }
}

public sealed class ApiClientUpsertRequest
{
    public int AgencyId { get; set; }
    public string ClientId { get; set; } = "";
    public string? ClientSecret { get; set; }
    public string? Name { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? SourceSystemCode { get; set; }
    public string[]? AllowedScopes { get; set; }
}

public sealed class RotateClientSecretRequest
{
    public string NewClientSecret { get; set; } = "";
}

public sealed class AuditStampDto
{
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

public sealed class GlobalSettingsUpdateRequest
{
    public JsonObject Settings { get; set; } = new();
    public AuditStampDto? Audit { get; set; }
}

public sealed class DeviceSettingsUpsertRequest
{
    public string DeviceId { get; set; } = "";
    public DeviceProfileDto? Profile { get; set; }
    public JsonObject Settings { get; set; } = new();
    public AuditStampDto? Audit { get; set; }
}

public sealed class DisplayRegistrationUpsertRequest
{
    public string DeviceId { get; set; } = "";
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public bool Enabled { get; set; } = true;
    public JsonObject Settings { get; set; } = new();
    public DeviceProfileDto? Profile { get; set; }
    public AuditStampDto? Audit { get; set; }
}

public sealed class DeviceBootstrapRequest
{
    public string DeviceId { get; set; } = "";
    public DeviceProfileDto? Profile { get; set; }
    public JsonObject? Settings { get; set; }
    public bool UpsertRegistration { get; set; } = true;
    public AuditStampDto? Audit { get; set; }
}

public sealed class DeviceCommandIssueRequest
{
    public string DeviceId { get; set; } = "";
    public string Command { get; set; } = "";
    public JsonObject? Payload { get; set; }
    public int? TimeoutSeconds { get; set; }
    public AuditStampDto? Audit { get; set; }
}

public sealed class DeviceCommandAckRequest
{
    public string Status { get; set; } = "acknowledged";
    public string? Message { get; set; }
    public JsonObject? Details { get; set; }
}

public sealed class DeviceHeartbeatRequest
{
    public string? Status { get; set; }
    public string? Message { get; set; }
    public JsonObject? Details { get; set; }
}
