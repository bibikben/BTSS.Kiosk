using System.Globalization;
using System.Text.RegularExpressions;

namespace BTSS.IntegrationTests;

public sealed class FireStationClearReport
{
    public string MessageId { get; set; } = string.Empty;
    public DateTime ReceivedUtc { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string RawBody { get; set; } = string.Empty;
    public DateTime DispatchTime { get; set; }
    public string Agency { get; set; } = string.Empty;
    public string DispatchGroup { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string CaseNumber { get; set; } = string.Empty;
    public string EventTypeCode { get; set; } = string.Empty;
    public string EventTypeText { get; set; } = string.Empty;
    public string EventSubtypeCode { get; set; } = string.Empty;
    public string EventSubtypeText { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Municipality { get; set; } = string.Empty;
    public string CrossStreet { get; set; } = string.Empty;
    public List<AssignedUnitStatus> AssignedUnitStatuses { get; set; } = new();
}

public sealed class AssignedUnitStatus
{
    public string Unit { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string Agency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
}

public sealed class FireStationClearReportParser
{
    private static readonly string[] DtFormats = { "MM-dd-yyyy  HH:mm:ss", "MM-dd-yyyy HH:mm:ss", "M-d-yyyy  HH:mm:ss", "M-d-yyyy HH:mm:ss" };
    private static readonly Regex AssignedUnitLineRegex = new(@"^\s*(?<unit>\S+)\s+(?<group>\S+)\s+(?<agency>\S+)\s+(?<status>[A-Z]{2})\s+(?<date>\d{2}-\d{2}-\d{4})\s+(?<time>\d{2}:\d{2}:\d{2})\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

    public FireStationClearReport Parse(string messageId, DateTime receivedUtc, string subject, string body)
    {
        var r = new FireStationClearReport
        {
            MessageId = messageId,
            ReceivedUtc = receivedUtc,
            Subject = subject ?? string.Empty,
            RawBody = body ?? string.Empty
        };

        r.DispatchTime = ParseHeaderDate(body, "Dispatch Time:") ?? DateTime.MinValue;
        r.Agency = ParseHeaderValue(body, "Agency:");
        r.DispatchGroup = ParseHeaderValue(body, "Dispatch Group:");
        r.EventId = ParseHeaderValue(body, "Event:");
        r.CaseNumber = ParseHeaderValue(body, "Case Number:");
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
        var idx = line.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        var raw = idx >= 0 ? line[(idx + label.Length)..].Trim() : line.Trim();
        if (DateTime.TryParseExact(raw, DtFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dt)) return dt;
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out dt)) return dt;
        return null;
    }

    private static (string code, string text) ParseCodeAndText(string body, string label)
    {
        var line = FindLineStartingWith(body, label);
        if (line is null) return (string.Empty, string.Empty);
        var raw = line[(line.IndexOf(label, StringComparison.OrdinalIgnoreCase) + label.Length)..].Trim();
        var parts = Regex.Split(raw, @"\s+").Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        if (parts.Count == 0) return (string.Empty, string.Empty);
        return (parts[0].Trim(), string.Join(" ", parts.Skip(1)).Trim());
    }

    private static string ParseHeaderValue(string body, string label)
    {
        var line = FindLineStartingWith(body, label);
        if (line is null) return string.Empty;
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
                if (addrLines.Count >= 3) break;
            }
            return string.Join(" ", addrLines).Trim();
        }
        return string.Empty;
    }

    private static string ExtractSection(string body, string header)
    {
        var normalized = body.Replace("\r\n", "\n").Replace("\r", "\n");
        var start = normalized.IndexOf(header, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return string.Empty;
        var remainder = normalized[start..];
        var nextHeader = remainder.IndexOf("\nEvent Comments:", StringComparison.OrdinalIgnoreCase);
        return nextHeader > 0 ? remainder[..nextHeader] : remainder;
    }

    public List<AssignedUnitStatus> ParseAssignedUnits(string emailBody)
    {
        if (string.IsNullOrWhiteSpace(emailBody)) return new();
        var block = ExtractSection(emailBody, "Assigned Units:");
        var matches = AssignedUnitLineRegex.Matches(block);
        var results = new List<AssignedUnitStatus>(matches.Count);
        foreach (Match match in matches)
        {
            var dtText = $"{match.Groups["date"].Value} {match.Groups["time"].Value}";
            if (!DateTime.TryParseExact(dtText, "MM-dd-yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
                continue;
            results.Add(new AssignedUnitStatus
            {
                Unit = match.Groups["unit"].Value.Trim(),
                Group = match.Groups["group"].Value.Trim(),
                Agency = match.Groups["agency"].Value.Trim(),
                Status = match.Groups["status"].Value.Trim(),
                TimestampUtc = dt
            });
        }
        return results;
    }
}
