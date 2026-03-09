using System.Text.Json.Nodes;

namespace BTSS.Service.Models;

public sealed class DeviceHeartbeatPayload
{
    public string DeviceId { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Message { get; set; }
    public JsonObject? Payload { get; set; }
}

public sealed class SyncAckEnvelope
{
    public long[] SyncLogIds { get; set; } = [];
    public string DeviceId { get; set; } = string.Empty;
    public string? BatchId { get; set; }
    public string? Notes { get; set; }
    public long? LastSyncLogId { get; set; }
}
