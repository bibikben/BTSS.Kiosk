using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using BTSS.IAR.Kiosk.DispatchEmail.Models;

namespace BTSS.IAR.Kiosk.DispatchEmail.Parsing;

public interface IFireStationClearReportParser
{
    FireStationClearReport Parse(string messageId, DateTimeOffset receivedUtc, string subject, string body);
}

/// <summary>
/// Best-effort parser. If your Fire Station Clear Report emails have a known schema,
/// replace the regex/JSON extraction with a strict model.
/// </summary>
public class FireStationClearReportParser : IFireStationClearReportParser
{
    public FireStationClearReport Parse(string messageId, DateTimeOffset receivedUtc, string subject, string body)
    {
        var report = new FireStationClearReport
        {
            MessageId = messageId,
            ReceivedUtc = receivedUtc,
            Subject = subject,
            RawBody = body
        };

        report.IncidentNumber = TryMatch(body, @"Incident\s*#\s*[:=]\s*(?<v>.+)$", "v");
        report.Station = TryMatch(body, @"Station\s*[:=]\s*(?<v>.+)$", "v");
        report.Location = TryMatch(body, @"Location\s*[:=]\s*(?<v>.+)$", "v");

        // 1) Try JSON extraction for assigned_unit_statuses
        var jsonStatuses = TryParseAssignedStatusesFromJson(body);
        if (jsonStatuses.Count > 0)
        {
            report.AssignedUnitStatuses = jsonStatuses;
            return report;
        }

        // 2) Fallback: parse line-based status entries
        report.AssignedUnitStatuses = ParseLineBasedStatuses(body);
        return report;
    }

    private static string? TryMatch(string text, string pattern, string group)
    {
        var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        return m.Success ? m.Groups[group].Value.Trim() : null;
    }

    private static List<AssignedUnitStatus> TryParseAssignedStatusesFromJson(string body)
    {
        var idx = body.IndexOf("assigned_unit_statuses", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return new();

        var start = body.LastIndexOf('{', idx);
        if (start < 0) return new();

        var json = body[start..].Trim();

        int depth = 0, end = -1;
        for (int i = 0; i < json.Length; i++)
        {
            if (json[i] == '{') depth++;
            else if (json[i] == '}')
            {
                depth--;
                if (depth == 0) { end = i; break; }
            }
        }
        if (end < 0) return new();

        var jsonObj = json[..(end + 1)];

        try
        {
            using var doc = JsonDocument.Parse(jsonObj);
            if (!doc.RootElement.TryGetProperty("assigned_unit_statuses", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return new();

            var list = new List<AssignedUnitStatus>();
            foreach (var el in arr.EnumerateArray())
            {
                var unit = el.TryGetProperty("unit", out var u) ? u.GetString() : null;
                var group = el.TryGetProperty("group", out var g) ? g.GetString() : null;
                var status = el.TryGetProperty("status", out var s) ? s.GetString() : null;

                DateTimeOffset ts = DateTimeOffset.MinValue;
                if (el.TryGetProperty("timestamp", out var t))
                {
                    var raw = t.GetString();
                    if (!DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out ts))
                        ts = DateTimeOffset.MinValue;
                }

                if (!string.IsNullOrWhiteSpace(unit) && !string.IsNullOrWhiteSpace(status))
                {
                    list.Add(new AssignedUnitStatus
                    {
                        Unit = unit!.Trim(),
                        Group = (group ?? "").Trim(),
                        Status = status!.Trim(),
                        TimestampUtc = ts == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : ts.ToUniversalTime()
                    });
                }
            }

            return list;
        }
        catch
        {
            return new();
        }
    }

    private static List<AssignedUnitStatus> ParseLineBasedStatuses(string body)
    {
        var list = new List<AssignedUnitStatus>();

        // Accepts lines like:
        // UNIT=E1 | GROUP=A | STATUS=ENROUTE | TIME=2026-02-17 13:42
        // Unit: E1  Group: A  Status: ENROUTE  Time: 2026-02-17T13:42:00Z
        var rx = new Regex(
            @"Unit\s*[:=]\s*(?<unit>[^|\r\n]+).*?Group\s*[:=]\s*(?<group>[^|\r\n]+).*?Status\s*[:=]\s*(?<status>[^|\r\n]+).*?(Time|Timestamp)\s*[:=]\s*(?<time>[^\r\n|]+)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match m in rx.Matches(body))
        {
            var unit = m.Groups["unit"].Value.Trim();
            var group = m.Groups["group"].Value.Trim();
            var status = m.Groups["status"].Value.Trim();
            var timeRaw = m.Groups["time"].Value.Trim();

            if (!DateTimeOffset.TryParse(timeRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var ts))
                ts = DateTimeOffset.UtcNow;

            if (!string.IsNullOrWhiteSpace(unit) && !string.IsNullOrWhiteSpace(status))
            {
                list.Add(new AssignedUnitStatus
                {
                    Unit = unit,
                    Group = group,
                    Status = status,
                    TimestampUtc = ts.ToUniversalTime()
                });
            }
        }

        return list;
    }
}
