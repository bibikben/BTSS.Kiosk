namespace BTSS.Service.Entities;

public sealed class LocalIncidentCommentEntity
{
    public long Id { get; set; }
    public string IncidentId { get; set; } = string.Empty;
    public long? ServerIncidentId { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime? OccurredAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? CreatedAgency { get; set; }
}

public sealed class LocalIncidentUnitEntity
{
    public long Id { get; set; }
    public string IncidentId { get; set; } = string.Empty;
    public long? ServerIncidentId { get; set; }
    public int? AgencyId { get; set; }
    public string UnitIdentifier { get; set; } = string.Empty;
    public string? Station { get; set; }
    public string? UnitType { get; set; }
    public string? CurrentStatus { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class LocalUnitTimelineFactEntity
{
    public long Id { get; set; }
    public string IncidentId { get; set; } = string.Empty;
    public long? ServerIncidentId { get; set; }
    public int? AgencyId { get; set; }
    public string UnitIdentifier { get; set; } = string.Empty;
    public DateTime? DispatchedAtUtc { get; set; }
    public DateTime? EnrouteAtUtc { get; set; }
    public DateTime? ArrivedAtUtc { get; set; }
    public DateTime? TransportBeginAtUtc { get; set; }
    public DateTime? TransportCompleteAtUtc { get; set; }
    public DateTime? ClearedAtUtc { get; set; }
    public DateTime? InQuartersAtUtc { get; set; }
}

public sealed class LocalSyncStateEntity
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTimeOffset? LastBootstrapAtUtc { get; set; }
    public DateTimeOffset? LastHeartbeatAtUtc { get; set; }
    public DateTimeOffset? LastAckAtUtc { get; set; }
    public DateTimeOffset? LastChangeAtUtc { get; set; }
    public long? LastSyncLogId { get; set; }
    public string? LastBatchId { get; set; }
    public string? ConflictPolicy { get; set; }
}

public sealed class LocalOutboxEntity
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Scope { get; set; } = "sync";
    public string ErrorCode { get; set; } = "outbox";
    public string Message { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
}
