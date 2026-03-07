namespace BTSS.Service.Entities;

public sealed class PollRunEntity
{
    public long Id { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset FinishedAtUtc { get; set; }
    public bool Succeeded { get; set; }
    public int HttpStatusCode { get; set; }
    public int IncidentCount { get; set; }
    public int PayloadBytes { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string? Error { get; set; }
}
