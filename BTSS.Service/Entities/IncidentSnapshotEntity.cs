namespace BTSS.Service.Entities;

public sealed class IncidentSnapshotEntity
{
    public int Id { get; set; }
    public string IncidentId { get; set; } = string.Empty;
    public string Agency { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string CallType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public DateTimeOffset? SourceUpdatedAtUtc { get; set; }
    public DateTimeOffset FirstSeenUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public string CanonicalJson { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
    public string? LastHash { get; set; }
}
