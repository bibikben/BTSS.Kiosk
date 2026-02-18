namespace BTSS.IAR.Kiosk.Services.DispatchEmail;

public record PivotTableResult(IReadOnlyList<string> StatusColumns, IReadOnlyList<PivotRow> Rows);

public record PivotRow(string Unit, string Group, Dictionary<string, string> StatusToTime);

public interface IPivotReportBuilder
{
    PivotTableResult Build(IReadOnlyList<DispatchAssignedUnitStatusEntity> statuses);
}

public class PivotReportBuilder : IPivotReportBuilder
{
    public PivotTableResult Build(IReadOnlyList<DispatchAssignedUnitStatusEntity> statuses)
    {
        var preferredOrder = new[] { "DP", "ER", "OS", "TR", "TC", "AM" };
        var presentStatuses = statuses
            .Select(s => (s.Status ?? "").Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Where(s => !string.Equals(s, "CU", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var statusCols = preferredOrder
            .Where(s => presentStatuses.Contains(s))
            .ToList();

        var groups = statuses
            .GroupBy(s => new { Unit = (s.Unit ?? "").Trim(), Group = (s.Group ?? "").Trim() })
            .OrderBy(g => g.Key.Unit, StringComparer.OrdinalIgnoreCase)
            .ThenBy(g => g.Key.Group, StringComparer.OrdinalIgnoreCase);

        var rows = new List<PivotRow>();
        foreach (var g in groups)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in statusCols)
            {
                var latest = g.Where(x => string.Equals(x.Status, col, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.TimestampUtc)
                    .FirstOrDefault();

                if (latest != null)
                    map[col] = DateTime.SpecifyKind(latest.TimestampUtc, DateTimeKind.Utc).ToLocalTime().ToString("HH:mm");
            }

            rows.Add(new PivotRow(g.Key.Unit, g.Key.Group, map));
        }

        return new PivotTableResult(statusCols, rows);
    }
}
