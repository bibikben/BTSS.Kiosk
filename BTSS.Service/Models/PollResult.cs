namespace BTSS.Service.Models;

public sealed record PollResult(
    IReadOnlyList<IncidentEnvelope> Incidents,
    int HttpStatusCode,
    int PayloadBytes,
    string Endpoint);
