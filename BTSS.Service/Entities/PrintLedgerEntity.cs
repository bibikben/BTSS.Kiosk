namespace BTSS.Service.Entities;

public sealed class PrintLedgerEntity
{
    public long Id { get; set; }
    public string IncidentId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public DateTimeOffset? PrintedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string TemplateKind { get; set; } = string.Empty;
    public string? PrinterName { get; set; }
    public string OutputPath { get; set; } = string.Empty;
    public string SummaryHash { get; set; } = string.Empty;
    public string PayloadSummary { get; set; } = string.Empty;
    public string? Error { get; set; }
}
