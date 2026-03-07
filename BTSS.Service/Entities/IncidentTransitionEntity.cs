namespace BTSS.Service.Entities;

public sealed class IncidentTransitionEntity
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
