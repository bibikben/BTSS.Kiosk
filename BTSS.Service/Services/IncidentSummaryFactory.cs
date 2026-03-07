using System.Text.Json;
using BTSS.IAR.Record.Models;
using BTSS.Service.Models;

namespace BTSS.Service.Services;

public static class IncidentSummaryFactory
{
    public static PrintableIncidentSummary Create(EmergencyCallUnified call, string canonicalJson)
    {
        var callers = call.Callers?
            .Where(c => !string.IsNullOrWhiteSpace(c.Name) || !string.IsNullOrWhiteSpace(c.PhoneNumber))
            .Select(c => string.Join(" | ", new[] { c.Name, c.PhoneNumber }.Where(x => !string.IsNullOrWhiteSpace(x))))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray() ?? [];

        var units = call.Units?
            .Where(u => !string.IsNullOrWhiteSpace(u.Id))
            .GroupBy(u => u.Id?.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var latest = g.OrderByDescending(x => x.CreatedAtISO ?? x.CreatedAt ?? x.Arrived ?? x.Enroute ?? x.Dispatched).First();
                var state = latest.Status ?? latest.StatusOriginal ?? "unknown";
                var station = string.IsNullOrWhiteSpace(latest.Station) ? null : latest.Station;
                return station is null ? $"{g.Key} ({state})" : $"{g.Key} ({state}, {station})";
            })
            .ToArray() ?? [];

        var comments = call.Comments?
            .Where(c => !string.IsNullOrWhiteSpace(c.Message))
            .OrderBy(c => c.CreatedAtISO ?? c.CreatedAt)
            .TakeLast(12)
            .Select(c => c.Message!.Replace("\r", " ").Replace("\n", " ").Trim())
            .ToArray() ?? [];

        return new PrintableIncidentSummary(
            IncidentId: ResolveIncidentId(call),
            Agency: ResolveAgency(call),
            Status: ResolveStatus(call),
            IsClosed: ResolveClosed(call),
            Address: ResolveAddress(call),
            CallType: ResolveCallType(call),
            Priority: call.Headers?.Priority ?? call.Agencies?.FirstOrDefault()?.Priority,
            DispatchGroup: call.Agencies?.FirstOrDefault()?.DispatchGroup,
            CaseNumber: call.CNum,
            EventNumber: call.Num1,
            CreatedAtUtc: ResolveCreatedAt(call),
            UpdatedAtUtc: ResolveUpdatedAt(call),
            CallerLines: callers,
            UnitLines: units,
            CommentLines: comments,
            CanonicalJson: canonicalJson);
    }

    public static string ResolveIncidentId(EmergencyCallUnified call) =>
        call.Details?.Id
        ?? call.Num1
        ?? call.CNum
        ?? call.Agencies?.FirstOrDefault()?.Id
        ?? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(call, EmergencyCallUnifiedJson.Options))));

    public static bool ResolveClosed(EmergencyCallUnified call) =>
        call.Details?.Closed
        ?? call.Agencies?.Any(x => x.Closed == true)
        ?? string.Equals(call.Details?.Status, "Closed", StringComparison.OrdinalIgnoreCase);

    public static string ResolveStatus(EmergencyCallUnified call) =>
        call.Details?.Status
        ?? (ResolveClosed(call) ? "Closed" : "Open");

    public static DateTimeOffset? ResolveUpdatedAt(EmergencyCallUnified call)
    {
        var value = call.Details?.UpdatedAt
                    ?? call.Details?.UpdatedAtISO
                    ?? call.Timestamp
                    ?? call.Agencies?.Select(x => x.UpdatedAt).Where(x => x.HasValue).Max();
        return value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;
    }

    public static DateTimeOffset? ResolveCreatedAt(EmergencyCallUnified call)
    {
        var value = call.Details?.CreatedAt
                    ?? call.Details?.CreatedAtISO
                    ?? call.Agencies?.Select(x => x.CreatedAt).Where(x => x.HasValue).Min();
        return value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)) : null;
    }

    public static string ResolveAgency(EmergencyCallUnified call) =>
        call.Details?.Agency
        ?? call.Agencies?.FirstOrDefault()?.Name
        ?? string.Empty;

    public static string ResolveAddress(EmergencyCallUnified call) =>
        call.Headers?.Address
        ?? call.Address
        ?? string.Empty;

    public static string ResolveCallType(EmergencyCallUnified call) =>
        call.Headers?.Type
        ?? call.Agencies?.FirstOrDefault()?.Type
        ?? string.Empty;
}
