using System.Globalization;
using System.Text.RegularExpressions;

namespace BTSS.IAR.Kiosk.Services.DispatchEmail;

public interface IFireStationClearReportParser
{
    FireStationClearReport Parse(string messageId, DateTimeOffset receivedUtc, string subject, string body);
}

/// <summary>
/// Parses the plain-text "Fire Station Clear Report" email format like the sample dispatch reports.
/// Supports the Assigned Units block with tab-separated columns.
/// </summary>
public class FireStationClearReportParser : IFireStationClearReportParser
{
    private static readonly string[] DtFormats =
    {
        "MM-dd-yyyy  HH:mm:ss", // note the double-space in many messages
        "MM-dd-yyyy HH:mm:ss",
        "M-d-yyyy  HH:mm:ss",
        "M-d-yyyy HH:mm:ss"
    };

    public FireStationClearReport Parse(string messageId, DateTimeOffset receivedUtc, string subject, string body)
    {
        var r = new FireStationClearReport
        {
            MessageId = messageId,
            ReceivedUtc = receivedUtc,
            Subject = subject ?? "",
            RawBody = body ?? ""
        };

        r.DispatchTime = ParseHeaderDate(body, "Dispatch Time:") ?? DateTime.MinValue;
        r.Agency = ParseHeaderValue(body, "Agency:");
        r.DispatchGroup = ParseHeaderValue(body, "Dispatch Group:");
        r.EventId = ParseHeaderValue(body, "Event:");
        r.CaseNumber = ParseHeaderValue(body, "Case Number:");

        // Event Type Code: "26C\tSICK PERSON"
        (r.EventTypeCode, r.EventTypeText) = ParseCodeAndText(body, "Event Type Code:");
        (r.EventSubtypeCode, r.EventSubtypeText) = ParseCodeAndText(body, "Event Subtype Code:");

        r.Address = ParseAddressBlock(body);
        r.Municipality = ParseHeaderValue(body, "Municipality:");
        r.CrossStreet = ParseHeaderValue(body, "Cross Street:");

        r.AssignedUnitStatuses = ParseAssignedUnits(body);
        return r;
    }

    private static DateTime? ParseHeaderDate(string body, string label)
    {
        var line = FindLineStartingWith(body, label);
        if (line is null) return null;

        // e.g. "Dispatch Time:\t07-19-2025  20:45:21"
        var idx = line.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        var raw = idx >= 0 ? line[(idx + label.Length)..].Trim() : line.Trim();

        if (DateTime.TryParseExact(raw, DtFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var dt))
            return dt;

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dt))
            return dt;

        return null;
    }

    private static (string code, string text) ParseCodeAndText(string body, string label)
    {
        var line = FindLineStartingWith(body, label);
        if (line is null) return ("", "");

        var raw = line[(line.IndexOf(label, StringComparison.OrdinalIgnoreCase) + label.Length)..].Trim();
        // raw can be: "26C\tSICK PERSON" or "ALRM\tALARM"
        var parts = Regex.Split(raw, @"\s+", RegexOptions.None)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (parts.Count == 0) return ("", "");
        var code = parts[0].Trim();
        var text = string.Join(" ", parts.Skip(1)).Trim();
        return (code, text);
    }

    private static string ParseHeaderValue(string body, string label)
    {
        var line = FindLineStartingWith(body, label);
        if (line is null) return "";

        var idx = line.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? line[(idx + label.Length)..].Trim() : line.Trim();
    }

    private static string? FindLineStartingWith(string body, string label)
    {
        foreach (var rawLine in body.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.TrimStart().StartsWith(label, StringComparison.OrdinalIgnoreCase))
                return line;
        }
        return null;
    }

    private static string ParseAddressBlock(string body)
    {
        // Address: then next one or two lines until a blank line or "Location Info:".
        var lines = body.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        for (int i = 0; i < lines.Count; i++)
        {
            if (!lines[i].Trim().Equals("Address:", StringComparison.OrdinalIgnoreCase))
                continue;

            var addrLines = new List<string>();
            for (int j = i + 1; j < lines.Count; j++)
            {
                var t = lines[j].Trim();
                if (string.IsNullOrWhiteSpace(t)) break;
                if (t.StartsWith("Location Info:", StringComparison.OrdinalIgnoreCase)) break;
                if (t.StartsWith("Municipality:", StringComparison.OrdinalIgnoreCase)) break;
                addrLines.Add(t);
                if (addrLines.Count >= 3) break; // avoid accidentally pulling other blocks
            }
            return string.Join(" ", addrLines).Trim();
        }
        return "";
    }

    private static List<AssignedUnitStatus> ParseAssignedUnits(string body)
    {
        var list = new List<AssignedUnitStatus>();
        var lines = body.Split('\n').Select(l => l.TrimEnd('\r')).ToList();

        int start = lines.FindIndex(l => l.Trim().Equals("Assigned Units:", StringComparison.OrdinalIgnoreCase));
        if (start < 0) return list;

        for (int i = start + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) break;
            if (line.TrimStart().StartsWith("Event Comments:", StringComparison.OrdinalIgnoreCase)) break;

            // Prefer tab-separated parsing (these reports use tabs and can have an empty Group column)
            var parts = line.Split('\t');
            var tokens = parts
                .Select(p => p.Trim())
                .Where(p => p.Length > 0 || parts.Length > 1) // keep empties if tabs exist
                .ToList();

            // If the line began with a tab, tokens[0] might be empty; remove leading empties
            while (tokens.Count > 0 && string.IsNullOrWhiteSpace(tokens[0]))
                tokens.RemoveAt(0);

            // Expected (tab format): Unit, Group (optional/empty), Agency, Status, DateTime
            // Some lines have empty Group (e.g., 4506B)
            if (tokens.Count < 5)
            {
                // fallback: split on whitespace (less reliable)
                tokens = Regex.Split(line.Trim(), @"\s+")
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();
                if (tokens.Count < 5) continue;
            }

            var unit = tokens[0];
            var group = tokens.Count >= 2 ? tokens[1] : "";
            var agency = tokens.Count >= 3 ? tokens[2] : "";
            var status = tokens.Count >= 4 ? tokens[3] : "";
            var dtRaw = tokens.Count >= 5 ? tokens[4] : "";

            // If whitespace split, date/time can be separate tokens
            if (tokens.Count >= 6 && Regex.IsMatch(tokens[5], @"\d{2}:\d{2}:\d{2}"))
                dtRaw = dtRaw + " " + tokens[5];

            if (string.IsNullOrWhiteSpace(unit) || string.IsNullOrWhiteSpace(status))
                continue;

            var tsUtc = ParseDispatchDateTime(dtRaw);
            if (tsUtc == null) tsUtc = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);

            list.Add(new AssignedUnitStatus
            {
                Unit = unit,
                Group = group,
                Agency = agency,
                Status = status,
                TimestampUtc = new DateTimeOffset(tsUtc.Value, TimeSpan.Zero)
            });
        }

        return list;
    }

    private static DateTime? ParseDispatchDateTime(string raw)
    {
        raw = (raw ?? "").Trim();
        if (raw.Length == 0) return null;

        if (DateTime.TryParseExact(raw, DtFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var dt))
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);

        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dt))
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);

        return null;
    }
}
