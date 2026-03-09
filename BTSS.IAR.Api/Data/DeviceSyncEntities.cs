using BTSS.IAR.Api.Models;
using System.Text.Json.Nodes;

namespace BTSS.IAR.Api.Data;

public sealed class DeviceHeartbeatEntity
{
    public long Id { get; set; }
    public long DeviceRefId { get; set; }
    public DeviceEntity? Device { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public int ApiClientId { get; set; }
    public int AgencyId { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public string? Status { get; set; }
    public string? Message { get; set; }
    public string PayloadJson { get; set; } = "{}";
}

public sealed class DeviceSyncStateEntity
{
    public long Id { get; set; }
    public long DeviceRefId { get; set; }
    public DeviceEntity? Device { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public int ApiClientId { get; set; }
    public int AgencyId { get; set; }
    public DateTime? LastBootstrapAtUtc { get; set; }
    public DateTime? LastHeartbeatAtUtc { get; set; }
    public DateTime? LastAckAtUtc { get; set; }
    public DateTime? LastChangeAtUtc { get; set; }
    public long? LastSyncLogId { get; set; }
    public string? LastBatchId { get; set; }
    public string? ConflictPolicy { get; set; }
    public string StateJson { get; set; } = "{}";
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SyncBatchEntity
{
    public long Id { get; set; }
    public string BatchId { get; set; } = Guid.NewGuid().ToString("N");
    public long DeviceRefId { get; set; }
    public DeviceEntity? Device { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public int AgencyId { get; set; }
    public int ApiClientId { get; set; }
    public string Direction { get; set; } = "download";
    public int ItemCount { get; set; }
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAtUtc { get; set; }
    public string Status { get; set; } = "started";
    public string? Notes { get; set; }
}

public sealed class SyncErrorEntity
{
    public long Id { get; set; }
    public long? DeviceRefId { get; set; }
    public DeviceEntity? Device { get; set; }
    public string? DeviceId { get; set; }
    public int? AgencyId { get; set; }
    public int? ApiClientId { get; set; }
    public string Scope { get; set; } = "sync";
    public string ErrorCode { get; set; } = "outbox";
    public string Message { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = "{}";
    public bool IsResolved { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }
}

public sealed record DeviceHeartbeatRequestDto(string DeviceId, string? Status, string? Message, JsonObject? Payload);
public sealed record DeviceIdentityDto(long Id, string DeviceId, string? DeviceName, string? MachineName, string? DeviceType, bool IsEnabled, int ApiClientId, int AgencyId, DateTime UpdatedAtUtc, JsonObject? Settings, DateTime? LastHeartbeatAtUtc, DateTime? LastAckAtUtc, string? ConflictPolicy);
public sealed record DeviceDisplayDto(string DeviceId, string? Name, string? Description, string? Location, bool Enabled, JsonObject? Settings, DeviceProfileDto? Profile);
public sealed record SyncBootstrapResponseDto(string BatchId, DeviceIdentityDto Device, int[] AgencyIds, object? Configuration, object[] Incidents, object[] Comments, object[] Units, object[] Timeline, DateTime ServerUtc);
public sealed record DeviceSyncChangeDto(long SyncLogId, long IncidentId, string Scope, string ChangeType, DateTime ChangedAtUtc, object? Incident, object[] Comments, object[] Units, object[] Timeline);
public sealed record SyncChangesResponseDto(string BatchId, string DeviceId, DateTime ServerUtc, DeviceSyncChangeDto[] Changes);
public sealed record SyncOutboxItemDto(string Scope, string ErrorCode, string Message, JsonObject? Payload, DateTime? OccurredAtUtc);
public sealed record SyncOutboxRequestDto(string DeviceId, List<SyncOutboxItemDto> Items);
public sealed record SyncAckEnvelopeDto(long[] SyncLogIds, string DeviceId, string? BatchId, string? Notes, long? LastSyncLogId);
