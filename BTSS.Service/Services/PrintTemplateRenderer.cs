using System.Text;
using System.Text.Json;
using BTSS.Service.Models;

namespace BTSS.Service.Services;

public sealed class PrintTemplateRenderer
{
    public string Render(PrintableIncidentSummary summary, string templateKind)
    {
        return templateKind switch
        {
            PrintTemplateKinds.DetailReprint => RenderDetail(summary),
            _ => RenderClosedSummary(summary)
        };
    }

    private static string RenderClosedSummary(PrintableIncidentSummary summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BTSS INCIDENT CLOSE SUMMARY");
        sb.AppendLine(new string('=', 32));
        AppendHeader(sb, summary);
        AppendSection(sb, "CALLERS", summary.CallerLines);
        AppendSection(sb, "UNITS", summary.UnitLines);
        AppendSection(sb, "COMMENTS", summary.CommentLines);
        return sb.ToString().TrimEnd();
    }

    private static string RenderDetail(PrintableIncidentSummary summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BTSS INCIDENT DETAIL REPRINT");
        sb.AppendLine(new string('=', 32));
        AppendHeader(sb, summary);
        AppendSection(sb, "CALLERS", summary.CallerLines);
        AppendSection(sb, "UNITS", summary.UnitLines);
        AppendSection(sb, "COMMENTS", summary.CommentLines);
        sb.AppendLine();
        sb.AppendLine("CANONICAL JSON");
        sb.AppendLine(new string('-', 32));
        using var doc = JsonDocument.Parse(summary.CanonicalJson);
        sb.AppendLine(JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true }));
        return sb.ToString().TrimEnd();
    }

    private static void AppendHeader(StringBuilder sb, PrintableIncidentSummary summary)
    {
        sb.AppendLine($"Incident ID : {summary.IncidentId}");
        sb.AppendLine($"Agency      : {summary.Agency}");
        sb.AppendLine($"Status      : {summary.Status}");
        sb.AppendLine($"Address     : {summary.Address}");
        sb.AppendLine($"Call Type   : {summary.CallType}");
        if (!string.IsNullOrWhiteSpace(summary.Priority)) sb.AppendLine($"Priority    : {summary.Priority}");
        if (!string.IsNullOrWhiteSpace(summary.DispatchGroup)) sb.AppendLine($"Group       : {summary.DispatchGroup}");
        if (!string.IsNullOrWhiteSpace(summary.CaseNumber)) sb.AppendLine($"Case Number : {summary.CaseNumber}");
        if (!string.IsNullOrWhiteSpace(summary.EventNumber)) sb.AppendLine($"Event       : {summary.EventNumber}");
        if (summary.CreatedAtUtc.HasValue) sb.AppendLine($"Created UTC : {summary.CreatedAtUtc:O}");
        if (summary.UpdatedAtUtc.HasValue) sb.AppendLine($"Updated UTC : {summary.UpdatedAtUtc:O}");
    }

    private static void AppendSection(StringBuilder sb, string name, IReadOnlyList<string> values)
    {
        if (values.Count == 0)
            return;

        sb.AppendLine();
        sb.AppendLine(name);
        sb.AppendLine(new string('-', 32));
        foreach (var value in values)
        {
            sb.AppendLine($"- {value}");
        }
    }
}
