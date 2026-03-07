using BTSS.IAR.Record.Models;

namespace BTSS.Service.Models;

public sealed record IncidentEnvelope(
    string IncidentId,
    bool IsClosed,
    string Status,
    DateTimeOffset? UpdatedAtUtc,
    string Agency,
    string Address,
    string CallType,
    EmergencyCallUnified Payload,
    string CanonicalJson,
    string SummaryText);
