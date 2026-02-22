using BTSS.IAR.Kiosk.DispatchEmail.Reporting;
using BTSS.IAR.Record.Models;

namespace BTSS.IAR.Kiosk.Services.IarApi;

public interface IIarPivotReportBuilder
{
    PivotTableResult Build(IarCallRecord record);
}

public sealed class IarPivotReportBuilder : IIarPivotReportBuilder
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "DP", "ER", "OS", "TR", "TC", "AM"
    };

    public PivotTableResult Build(IarCallRecord record)
    {
        var units = record.Units ?? Array.Empty<IarUnit>();

        // Columns in the order you specified (and used elsewhere)
        var cols = new List<string> { "DP", "ER", "OS", "TR", "TC", "AM" };

        var byUnit = units
            .Where(u => !string.IsNullOrWhiteSpace(u.Id))
            .GroupBy(u => u.Id!.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        var rows = new List<PivotRow>();

        foreach (var g in byUnit)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var u in g)
            {
                var status = (u.StatusOriginal ?? u.Status ?? "").Trim().ToUpperInvariant();
                if (!AllowedStatuses.Contains(status))
                    continue; // exclude CU + anything else

                var ts = u.CreatedAt ?? u.CreatedAtISO;
                if (ts == null) continue;

                // Keep the latest time for that status
                if (!map.TryGetValue(status, out var existing))
                {
                    map[status] = ts.Value.ToLocalTime().ToString("HH:mm");
                }
                else
                {
                    // Compare by actual timestamp (we'll re-parse HH:mm only if needed)
                    var candidate = ts.Value;
                    var latest = g
                        .Where(x => string.Equals((x.StatusOriginal ?? x.Status ?? "").Trim(), status, StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.CreatedAt ?? x.CreatedAtISO)
                        .Where(x => x != null)
                        .Max();
                    if (latest != null)
                        map[status] = latest.Value.ToLocalTime().ToString("HH:mm");
                }
            }

            // Group column is not present in IAR payload; keep empty to align print layout.
            rows.Add(new PivotRow(g.Key, "", map));
        }

        return new PivotTableResult(cols, rows);
    }
}
