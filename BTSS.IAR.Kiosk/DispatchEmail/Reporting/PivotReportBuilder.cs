using BTSS.IAR.Kiosk.DispatchEmail.Data;
using BTSS.IAR.Kiosk.Services.DispatchEmail;

namespace BTSS.IAR.Kiosk.DispatchEmail.Reporting;

public record PivotTableResult(
    IReadOnlyList<string> StatusColumns,
    IReadOnlyList<PivotRow> Rows
);

public record PivotRow(
    string Unit,
    string Group,
    Dictionary<string, string> StatusToTime // HH:mm values
);

public interface IPivotReportBuilder
{
    PivotTableResult Build(List<AssignedUnitStatusEntity> statuses);
}

public class PivotReportBuilder : IPivotReportBuilder
{
    public PivotTableResult Build(List<AssignedUnitStatusEntity> statuses)
    {
        var statusCols = statuses
            .Select(s => (s.Status ?? "").Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var grouped = statuses
            .GroupBy(s => new { Unit = (s.Unit ?? "").Trim(), Group = (s.Group ?? "").Trim() })
            .OrderBy(g => g.Key.Unit, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Key.Group, StringComparer.OrdinalIgnoreCase);

        var rows = new List<PivotRow>();

        foreach (var g in grouped)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var status in statusCols)
            {
                var latest = g
                    .Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.TimestampUtc)
                    .FirstOrDefault();

                if (latest != null)
                    map[status] = latest.TimestampUtc.ToLocalTime().ToString("HH:mm");
            }

            rows.Add(new PivotRow(g.Key.Unit, g.Key.Group, map));
        }

        return new PivotTableResult(statusCols, rows);
    }
}
