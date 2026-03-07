namespace BTSS.Service.Models;

public sealed record PrintableIncidentSummary(
    string IncidentId,
    string Agency,
    string Status,
    bool IsClosed,
    string Address,
    string CallType,
    string? Priority,
    string? DispatchGroup,
    string? CaseNumber,
    string? EventNumber,
    DateTimeOffset? CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<string> CallerLines,
    IReadOnlyList<string> UnitLines,
    IReadOnlyList<string> CommentLines,
    string CanonicalJson);

public static class PrintTemplateKinds
{
    public const string ClosedSummary = "closed-summary";
    public const string DetailReprint = "detail-reprint";
}
