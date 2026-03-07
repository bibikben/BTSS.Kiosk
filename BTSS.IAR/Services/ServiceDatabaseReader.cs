using BTSS.IAR.Models;
using BTSS.IAR.Record.Models;
using Microsoft.Data.Sqlite;

namespace BTSS.IAR.Services;

public sealed class ServiceDatabaseReader
{
    public async Task<DashboardSnapshot> LoadAsync(string dbPath, CancellationToken cancellationToken = default)
    {
        var snapshot = new DashboardSnapshot();
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
            return snapshot;

        var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        snapshot.OpenIncidents = await ReadIncidentsAsync(connection, false, cancellationToken);
        snapshot.ClosedIncidents = await ReadIncidentsAsync(connection, true, cancellationToken);
        snapshot.PrintJobs = await ReadPrintJobsAsync(connection, cancellationToken);
        snapshot.PollRuns = await ReadPollRunsAsync(connection, cancellationToken);
        snapshot.RecentTransitions = await ReadRecentTransitionsAsync(connection, cancellationToken);
        return snapshot;
    }

    public async Task<IncidentDetailView?> LoadIncidentDetailAsync(string dbPath, string incidentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath) || string.IsNullOrWhiteSpace(incidentId))
            return null;

        var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var snapshot = await ReadIncidentAsync(connection, incidentId, cancellationToken);
        if (snapshot is null)
            return null;

        var detail = new IncidentDetailView
        {
            Snapshot = snapshot,
            Timeline = await ReadTransitionsAsync(connection, incidentId, cancellationToken)
        };

        HydrateFromCanonicalJson(detail);
        return detail;
    }

    public async Task<ReportsSnapshot> LoadReportsAsync(string dbPath, ReportFilterOptions? filters = null, CancellationToken cancellationToken = default)
    {
        var report = new ReportsSnapshot { GeneratedAtUtc = DateTimeOffset.UtcNow };
        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
            return report;

        var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var transitionsByIncident = await ReadAllTransitionsAsync(connection, cancellationToken);
        var printJobsByIncident = await ReadAllPrintJobsAsync(connection, cancellationToken);
        var closedIncidents = await ReadIncidentsAsync(connection, true, cancellationToken);
        var summaryRows = BuildSummaryRows(closedIncidents, transitionsByIncident, printJobsByIncident, filters);

        report.SummaryRows = summaryRows;
        report.MonthlyRows = BuildMonthlyRows(summaryRows, printJobsByIncident);
        report.TotalsByAgency = summaryRows
            .GroupBy(x => x.Agency ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        report.TotalsByCallType = summaryRows
            .GroupBy(x => x.CallType ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        return report;
    }

    public async Task<ReportDetailModel?> LoadReportDetailAsync(string dbPath, string incidentId, CancellationToken cancellationToken = default)
    {
        var detail = await LoadIncidentDetailAsync(dbPath, incidentId, cancellationToken);
        if (detail is null)
            return null;

        var closedAt = detail.Timeline
            .Where(x => x.ToClosed)
            .OrderByDescending(x => x.ObservedAtUtc)
            .Select(x => (DateTimeOffset?)x.ObservedAtUtc)
            .FirstOrDefault();

        return new ReportDetailModel
        {
            IncidentId = detail.Snapshot.IncidentId,
            Agency = detail.Snapshot.Agency,
            CallType = detail.Snapshot.CallType,
            Status = detail.Snapshot.Status,
            Address = detail.Snapshot.Address,
            LocationName = detail.LocationName,
            Priority = detail.Priority,
            Coordinates = detail.Coordinates,
            CallerSummary = detail.CallerSummary,
            FirstSeenUtc = detail.Snapshot.FirstSeenUtc,
            LastSeenUtc = detail.Snapshot.LastSeenUtc,
            ClosedAtUtc = closedAt,
            SummaryText = detail.Snapshot.SummaryText,
            Timeline = detail.Timeline,
            Units = detail.Units,
            Comments = detail.Comments
        };
    }

    private static List<ReportSummaryRow> BuildSummaryRows(
        IEnumerable<IncidentListItem> incidents,
        IReadOnlyDictionary<string, List<IncidentTransitionItem>> transitionsByIncident,
        IReadOnlyDictionary<string, List<PrintJobItem>> printJobsByIncident,
        ReportFilterOptions? filters)
    {
        var list = new List<ReportSummaryRow>();

        foreach (var incident in incidents)
        {
            var transitions = transitionsByIncident.TryGetValue(incident.IncidentId, out var transitionRows)
                ? transitionRows
                : new List<IncidentTransitionItem>();

            var detail = new IncidentDetailView
            {
                Snapshot = incident,
                Timeline = transitions
            };
            HydrateFromCanonicalJson(detail);

            var closedAt = transitions
                .Where(x => x.ToClosed)
                .OrderByDescending(x => x.ObservedAtUtc)
                .Select(x => (DateTimeOffset?)x.ObservedAtUtc)
                .FirstOrDefault() ?? incident.LastSeenUtc;

            var summary = new ReportSummaryRow
            {
                IncidentId = incident.IncidentId,
                Agency = incident.Agency,
                CallType = incident.CallType,
                Address = incident.Address,
                Status = incident.Status,
                ClosedAtUtc = closedAt,
                FirstSeenUtc = incident.FirstSeenUtc,
                LastSeenUtc = incident.LastSeenUtc,
                PrimaryCaller = detail.CallerSummary,
                UnitSummary = string.Join(", ", detail.Units.Select(x => x.Id).Where(static x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Take(8)),
                CommentCount = detail.Comments.Count,
                TransitionCount = transitions.Count,
                MinutesOpen = CalculateMinutesOpen(incident.FirstSeenUtc, closedAt ),
                SummaryText = incident.SummaryText
            };

            if (!MatchesFilters(summary, filters))
                continue;

            list.Add(summary);
        }

        return list
            .OrderByDescending(x => x.ClosedAtUtc ?? x.LastSeenUtc ?? DateTimeOffset.MinValue)
            .ThenByDescending(x => x.LastSeenUtc ?? DateTimeOffset.MinValue)
            .ToList();
    }

    private static List<MonthlyReportRow> BuildMonthlyRows(
        IEnumerable<ReportSummaryRow> rows,
        IReadOnlyDictionary<string, List<PrintJobItem>> printJobsByIncident)
    {
        return rows
            .GroupBy(row => new
            {
                MonthKey = (row.ClosedAtUtc ?? row.LastSeenUtc ?? DateTimeOffset.MinValue).ToLocalTime().ToString("yyyy-MM"),
                row.Agency,
                row.CallType
            })
            .Select(group =>
            {
                var printRows = group
                    .SelectMany(x => printJobsByIncident.TryGetValue(x.IncidentId, out var jobs) && jobs != null ? jobs : Enumerable.Empty<PrintJobItem>())
                    .ToList();

                return new MonthlyReportRow
                {
                    MonthKey = group.Key.MonthKey,
                    Agency = group.Key.Agency,
                    CallType = group.Key.CallType,
                    IncidentCount = group.Count(),
                    TotalMinutesOpen = group.Sum(x => x.MinutesOpen),
                    AverageMinutesOpen = group.Any() ? (int)Math.Round(group.Average(x => x.MinutesOpen), MidpointRounding.AwayFromZero) : 0,
                    PrintedCount = printRows.Count(x => string.Equals(x.Status, "Printed", StringComparison.OrdinalIgnoreCase)),
                    FailedPrintCount = printRows.Count(x => !string.Equals(x.Status, "Printed", StringComparison.OrdinalIgnoreCase))
                };
            })
            .OrderByDescending(x => x.MonthKey)
            .ThenBy(x => x.Agency)
            .ThenBy(x => x.CallType)
            .ToList();
    }

    private static bool MatchesFilters(ReportSummaryRow row, ReportFilterOptions? filters)
    {
        if (filters is null)
            return true;

        if (!string.IsNullOrWhiteSpace(filters.SearchText))
        {
            var haystack = string.Join(" ", new[] { row.IncidentId, row.Agency, row.CallType, row.Address, row.PrimaryCaller, row.UnitSummary, row.SummaryText });
            if (!haystack.Contains(filters.SearchText.Trim(), StringComparison.OrdinalIgnoreCase))
                return false;
        }

        if (!string.IsNullOrWhiteSpace(filters.Agency) && !string.Equals(row.Agency, filters.Agency, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrWhiteSpace(filters.CallType) && !string.Equals(row.CallType, filters.CallType, StringComparison.OrdinalIgnoreCase))
            return false;

        var rowDate = (row.ClosedAtUtc ?? row.LastSeenUtc ?? row.FirstSeenUtc)?.LocalDateTime.Date;
        if (filters.StartDate.HasValue && rowDate.HasValue && rowDate.Value < filters.StartDate.Value.Date)
            return false;
        if (filters.EndDate.HasValue && rowDate.HasValue && rowDate.Value > filters.EndDate.Value.Date)
            return false;

        return true;
    }

    private static int CalculateMinutesOpen(DateTimeOffset? startedAt, DateTimeOffset? endedAt)
    {
        if (!startedAt.HasValue || !endedAt.HasValue || endedAt.Value < startedAt.Value)
            return 0;

        return (int)Math.Round((endedAt.Value - startedAt.Value).TotalMinutes, MidpointRounding.AwayFromZero);
    }

    private static async Task<Dictionary<string, List<IncidentTransitionItem>>> ReadAllTransitionsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT Id, IncidentId, FromStatus, ToStatus, FromClosed, ToClosed, ObservedAtUtc, Notes
                             FROM Transitions
                             ORDER BY ObservedAtUtc DESC;";
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var map = new Dictionary<string, List<IncidentTransitionItem>>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = ReadTransition(reader);
            if (!map.TryGetValue(item.IncidentId, out var list))
            {
                list = new List<IncidentTransitionItem>();
                map[item.IncidentId] = list;
            }
            list.Add(item);
        }
        return map;
    }

    private static async Task<Dictionary<string, List<PrintJobItem>>> ReadAllPrintJobsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT IncidentId, Status, TemplateKind, OutputPath, AttemptCount, CreatedAtUtc, LastAttemptAtUtc, PrintedAtUtc, Error
                             FROM PrintLedger
                             ORDER BY CreatedAtUtc DESC;";
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var map = new Dictionary<string, List<PrintJobItem>>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            var item = new PrintJobItem
            {
                IncidentId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                Status = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                TemplateKind = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                OutputPath = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                AttemptCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                CreatedAtUtc = reader.IsDBNull(5) ? DateTimeOffset.MinValue : DateTimeOffset.Parse(reader.GetString(5)),
                LastAttemptAtUtc = reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6)),
                PrintedAtUtc = reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)),
                Error = reader.IsDBNull(8) ? null : reader.GetString(8)
            };

            if (!map.TryGetValue(item.IncidentId, out var list))
            {
                list = new List<PrintJobItem>();
                map[item.IncidentId] = list;
            }
            list.Add(item);
        }
        return map;
    }

    private static async Task<IncidentListItem?> ReadIncidentAsync(SqliteConnection connection, string incidentId, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT IncidentId, Agency, Address, CallType, Status, IsClosed, SourceUpdatedAtUtc, FirstSeenUtc, LastSeenUtc, SummaryText, CanonicalJson
                             FROM Incidents
                             WHERE IncidentId = $incidentId
                             LIMIT 1;";
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$incidentId", incidentId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadIncidentListItem(reader);
    }

    private static async Task<List<IncidentListItem>> ReadIncidentsAsync(SqliteConnection connection, bool closed, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT IncidentId, Agency, Address, CallType, Status, IsClosed, SourceUpdatedAtUtc, FirstSeenUtc, LastSeenUtc, SummaryText, CanonicalJson
                             FROM Incidents
                             WHERE IsClosed = $closed
                             ORDER BY COALESCE(SourceUpdatedAtUtc, LastSeenUtc) DESC
                             LIMIT 200;";
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$closed", closed ? 1 : 0);

        var list = new List<IncidentListItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            list.Add(ReadIncidentListItem(reader));

        return list;
    }

    private static IncidentListItem ReadIncidentListItem(SqliteDataReader reader)
    {
        return new IncidentListItem
        {
            IncidentId = reader.GetString(0),
            Agency = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            Address = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            CallType = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Status = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            IsClosed = reader.GetBoolean(5),
            SourceUpdatedAtUtc = reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6)),
            FirstSeenUtc = reader.IsDBNull(7) ? DateTimeOffset.MinValue : DateTimeOffset.Parse(reader.GetString(7)),
            LastSeenUtc = reader.IsDBNull(8) ? DateTimeOffset.MinValue : DateTimeOffset.Parse(reader.GetString(8)),
            SummaryText = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
            CanonicalJson = reader.IsDBNull(10) ? string.Empty : reader.GetString(10)
        };
    }

    private static async Task<List<IncidentTransitionItem>> ReadRecentTransitionsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT Id, IncidentId, FromStatus, ToStatus, FromClosed, ToClosed, ObservedAtUtc, Notes
                             FROM Transitions
                             ORDER BY ObservedAtUtc DESC
                             LIMIT 100;";
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var list = new List<IncidentTransitionItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            list.Add(ReadTransition(reader));
        return list;
    }

    private static async Task<List<IncidentTransitionItem>> ReadTransitionsAsync(SqliteConnection connection, string incidentId, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT Id, IncidentId, FromStatus, ToStatus, FromClosed, ToClosed, ObservedAtUtc, Notes
                             FROM Transitions
                             WHERE IncidentId = $incidentId
                             ORDER BY ObservedAtUtc DESC
                             LIMIT 100;";
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$incidentId", incidentId);
        var list = new List<IncidentTransitionItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            list.Add(ReadTransition(reader));
        return list;
    }

    private static IncidentTransitionItem ReadTransition(SqliteDataReader reader)
    {
        return new IncidentTransitionItem
        {
            Id = reader.IsDBNull(0) ? 0 : reader.GetInt64(0),
            IncidentId = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
            FromStatus = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            ToStatus = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            FromClosed = !reader.IsDBNull(4) && reader.GetBoolean(4),
            ToClosed = !reader.IsDBNull(5) && reader.GetBoolean(5),
            ObservedAtUtc = reader.IsDBNull(6) ? DateTimeOffset.MinValue : DateTimeOffset.Parse(reader.GetString(6)),
            Notes = reader.IsDBNull(7) ? null : reader.GetString(7)
        };
    }

    private static async Task<List<PrintJobItem>> ReadPrintJobsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT IncidentId, Status, TemplateKind, OutputPath, AttemptCount, CreatedAtUtc, LastAttemptAtUtc, PrintedAtUtc, Error
                             FROM PrintLedger
                             ORDER BY CreatedAtUtc DESC
                             LIMIT 50;";
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var list = new List<PrintJobItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new PrintJobItem
            {
                IncidentId = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                Status = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                TemplateKind = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                OutputPath = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                AttemptCount = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                CreatedAtUtc = reader.IsDBNull(5) ? DateTimeOffset.MinValue : DateTimeOffset.Parse(reader.GetString(5)),
                LastAttemptAtUtc = reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6)),
                PrintedAtUtc = reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7)),
                Error = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }
        return list;
    }

    private static async Task<List<PollRunItem>> ReadPollRunsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT StartedAtUtc, FinishedAtUtc, Succeeded, IncidentCount, Error, HttpStatusCode, PayloadBytes, Endpoint
                             FROM PollRuns
                             ORDER BY StartedAtUtc DESC
                             LIMIT 50;";
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var list = new List<PollRunItem>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new PollRunItem
            {
                StartedAtUtc = reader.IsDBNull(0) ? DateTimeOffset.MinValue : DateTimeOffset.Parse(reader.GetString(0)),
                CompletedAtUtc = reader.IsDBNull(1) ? null : DateTimeOffset.Parse(reader.GetString(1)),
                Success = !reader.IsDBNull(2) && reader.GetBoolean(2),
                IncidentCount = reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                Error = reader.IsDBNull(4) ? null : reader.GetString(4),
                HttpStatusCode = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                PayloadBytes = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
                Endpoint = reader.IsDBNull(7) ? string.Empty : reader.GetString(7)
            });
        }
        return list;
    }

    private static void HydrateFromCanonicalJson(IncidentDetailView detail)
    {
        var call = EmergencyCallUnifiedJson.Deserialize(detail.Snapshot.CanonicalJson);
        if (call is null)
        {
            detail.QuerySeed = detail.Snapshot.IncidentId;
            return;
        }

        detail.Priority = call.GetPriority() ?? string.Empty;
        detail.LocationName = call.GetLocationName() ?? string.Empty;
        var lat = call.GetLatitude();
        var lng = call.GetLongitude();
        detail.Coordinates = lat.HasValue && lng.HasValue
            ? $"{lat.Value:F6}, {lng.Value:F6}"
            : string.Empty;

        detail.CallerSummary = string.Join(" | ",
            (call.Callers ?? Array.Empty<Caller>())
                .Select(c => string.Join(" ", new[] { c.Name, c.PhoneNumber }.Where(v => !string.IsNullOrWhiteSpace(v))))
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Take(3));

        detail.Units = (call.Units ?? Array.Empty<Unit>())
            .Select(unit => new IncidentUnitItem
            {
                Id = unit.Id ?? string.Empty,
                Status = unit.NormalizeUnitStatus(),
                Agency = unit.Agency ?? string.Empty,
                Station = unit.Station ?? string.Empty,
                Type = unit.Type ?? string.Empty,
                TimestampUtc = unit.GetBestTimestampUtc() is DateTime utc ? new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)) : null
            })
            .OrderByDescending(x => x.TimestampUtc ?? DateTimeOffset.MinValue)
            .ThenBy(x => x.Id)
            .ToList();

        detail.Comments = (call.Comments ?? Array.Empty<Comment>())
            .Select(comment => new IncidentCommentItem
            {
                Message = comment.Message ?? string.Empty,
                CreatedBy = comment.CreatedBy ?? string.Empty,
                CreatedAgency = comment.CreatedAgency ?? string.Empty,
                CreatedAtUtc = new DateTimeOffset(comment.CreatedAt ?? (comment.CreatedAtISO ?? DateTime.UtcNow))
            })
            .OrderByDescending(x => x.CreatedAtUtc ?? DateTimeOffset.MinValue)
            .ToList();

        detail.QuerySeed = string.Join(" ", new[]
        {
            detail.Snapshot.IncidentId,
            detail.Snapshot.CallType,
            detail.Snapshot.Address,
            detail.LocationName,
            detail.CallerSummary
        }.Where(static x => !string.IsNullOrWhiteSpace(x)));
    }
}
