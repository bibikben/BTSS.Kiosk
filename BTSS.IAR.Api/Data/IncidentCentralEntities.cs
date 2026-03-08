using System.ComponentModel.DataAnnotations.Schema;

namespace BTSS.IAR.Api.Data;

public sealed class IncidentEntity
{
    public long Id { get; set; }
    public string ExternalIncidentId { get; set; } = string.Empty;
    public short? SourceSystemId { get; set; }
    public SourceSystemEntity? SourceSystem { get; set; }
    public int? AgencyPrimaryId { get; set; }
    public Agency? AgencyPrimary { get; set; }
    public string? Type { get; set; }
    public string? Priority { get; set; }
    public string? Address { get; set; }
    public string? LocationName { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Coordinates { get; set; }
    public string? Status { get; set; }
    public DateTime? DispatchedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string RawPayloadJson { get; set; } = "{}";
    public DateTime? SourceCreatedAtUtc { get; set; }
    public DateTime? SourceUpdatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class IncidentAgencyEntity
{
    public long Id { get; set; }
    public long IncidentId { get; set; }
    public IncidentEntity? Incident { get; set; }
    public int AgencyId { get; set; }
    public Agency? Agency { get; set; }
    public string? AgencyEventId { get; set; }
    public string? DispatchGroup { get; set; }
    public string? CaseNumber { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class IncidentCallerEntity
{
    public long Id { get; set; }
    public long IncidentId { get; set; }
    public IncidentEntity? Incident { get; set; }
    public string? Name { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public bool? FirstCall { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class IncidentCommentEntity
{
    public long Id { get; set; }
    public long IncidentId { get; set; }
    public IncidentEntity? Incident { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime? OccurredAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedAgency { get; set; }
}

public sealed class IncidentUnitEntity
{
    public long Id { get; set; }
    public long IncidentId { get; set; }
    public IncidentEntity? Incident { get; set; }
    public int? AgencyId { get; set; }
    public Agency? Agency { get; set; }
    public string UnitIdentifier { get; set; } = string.Empty;
    public string? Station { get; set; }
    public string? UnitType { get; set; }
    public string? CurrentStatus { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class UnitStatusEventEntity
{
    public long Id { get; set; }
    public long IncidentId { get; set; }
    public IncidentEntity? Incident { get; set; }
    public int? AgencyId { get; set; }
    public Agency? Agency { get; set; }
    public string UnitIdentifier { get; set; } = string.Empty;
    public string? StatusCodeRaw { get; set; }
    public string? StatusCodeNormalized { get; set; }
    public DateTime? OccurredAtUtc { get; set; }
    public string? SourceText { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedAgency { get; set; }
}

public sealed class UnitTimelineFactEntity
{
    public long Id { get; set; }
    public long IncidentId { get; set; }
    public IncidentEntity? Incident { get; set; }
    public int? AgencyId { get; set; }
    public Agency? Agency { get; set; }
    public string UnitIdentifier { get; set; } = string.Empty;
    public DateTime? DispatchedAtUtc { get; set; }
    public DateTime? EnrouteAtUtc { get; set; }
    public DateTime? ArrivedAtUtc { get; set; }
    public DateTime? TransportBeginAtUtc { get; set; }
    public DateTime? TransportCompleteAtUtc { get; set; }
    public DateTime? ClearedAtUtc { get; set; }
    public DateTime? InQuartersAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class IncidentAssignmentEntity
{
    public long Id { get; set; }
    public long IncidentId { get; set; }
    public IncidentEntity? Incident { get; set; }
    public int AgencyId { get; set; }
    public string? DeviceId { get; set; }
    public string? AssignmentKind { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class IncidentSyncLogEntity
{
    public long Id { get; set; }
    public long IncidentId { get; set; }
    public IncidentEntity? Incident { get; set; }
    public int AgencyId { get; set; }
    public string Scope { get; set; } = "incident";
    public string ChangeType { get; set; } = "upsert";
    public DateTime ChangedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AckedAtUtc { get; set; }
    public string? DeviceId { get; set; }
    public string? Notes { get; set; }
}

public sealed class DeviceEntity
{
    public long Id { get; set; }
    public int ApiClientId { get; set; }
    public ApiClient? ApiClient { get; set; }
    public int AgencyId { get; set; }
    public Agency? Agency { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public string? MachineName { get; set; }
    public string? DeviceType { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class DeviceAgencyEntity
{
    public long Id { get; set; }
    public long DeviceRefId { get; set; }
    public DeviceEntity? Device { get; set; }
    public int AgencyId { get; set; }
    public Agency? Agency { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class DeviceSettingEntity
{
    public long Id { get; set; }
    public long? DeviceRefId { get; set; }
    public DeviceEntity? Device { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public int? AgencyId { get; set; }
    public string SettingsJson { get; set; } = "{}";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class GlobalSettingEntity
{
    public long Id { get; set; }
    public int AgencyId { get; set; }
    public Agency? Agency { get; set; }
    public string SettingsJson { get; set; } = "{}";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed record IncidentAgencySummaryDto(int AgencyId, string? AgencyCode, string? AgencyName, string? AgencyEventId, string? DispatchGroup, string? CaseNumber, bool IsPrimary);
public sealed record IncidentCallerDto(string? Name, string? PhoneNumber, string? Address, string? City, bool? FirstCall, DateTime? CreatedAtUtc);
public sealed record IncidentCommentDto(long Id, string Message, DateTime? OccurredAtUtc, string? CreatedBy, string? CreatedAgency);
public sealed record IncidentUnitDto(long Id, int? AgencyId, string UnitIdentifier, string? Station, string? UnitType, string? CurrentStatus, DateTime? CreatedAtUtc, DateTime? UpdatedAtUtc);
public sealed record UnitStatusEventDto(long Id, int? AgencyId, string UnitIdentifier, string? StatusCodeRaw, string? StatusCodeNormalized, DateTime? OccurredAtUtc, string? SourceText, string? CreatedBy, string? CreatedAgency);
public sealed record UnitTimelineFactDto(long Id, int? AgencyId, string UnitIdentifier, DateTime? DispatchedAtUtc, DateTime? EnrouteAtUtc, DateTime? ArrivedAtUtc, DateTime? TransportBeginAtUtc, DateTime? TransportCompleteAtUtc, DateTime? ClearedAtUtc, DateTime? InQuartersAtUtc);
public sealed record IncidentListItemDto(long Id, string ExternalIncidentId, string? Type, string? Priority, string? Address, string? LocationName, string? Status, DateTime? DispatchedAtUtc, DateTime? ClosedAtUtc, DateTime UpdatedAtUtc, int AgencyCount);
public sealed record IncidentDetailDto(long Id, string ExternalIncidentId, short? SourceSystemId, int? AgencyPrimaryId, string? Type, string? Priority, string? Address, string? LocationName, double? Latitude, double? Longitude, string? Coordinates, string? Status, DateTime? DispatchedAtUtc, DateTime? ClosedAtUtc, DateTime? SourceCreatedAtUtc, DateTime? SourceUpdatedAtUtc, DateTime CreatedAtUtc, DateTime UpdatedAtUtc, IncidentAgencySummaryDto[] Agencies, IncidentCallerDto[] Callers);
public sealed record SyncAckRequest(long[] SyncLogIds, string? DeviceId, string? Notes);

public static class IncidentStatusNormalizer
{
    public static string? Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        return raw.Trim().ToUpperInvariant() switch
        {
            "DP" or "DISPATCHED" => "Dispatched",
            "ER" or "ENROUTE" => "Enroute",
            "OS" or "ONSCENE" or "ONSCENE " => "Arrived",
            "TR" => "Transport Begin",
            "TC" => "Transport Complete",
            "CU" or "CL" => "Cleared",
            "AV" or "AM" or "AVAILABLE" or "AK" or "QUARTERS" => "In Quarters",
            _ => raw.Trim()
        };
    }
}
