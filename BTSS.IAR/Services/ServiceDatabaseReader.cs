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
