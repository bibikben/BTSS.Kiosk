using SQLite;
using BTSS.IAR.Kiosk.DispatchEmail.Models;

namespace BTSS.IAR.Kiosk.DispatchEmail.Data;

public interface IReportRepository
{
    Task InitializeAsync();
    Task<bool> ExistsByMessageIdAsync(string messageId);
    Task<int> InsertReportAsync(FireStationClearReport report);
    Task<List<AssignedUnitStatusEntity>> GetStatusesAsync(int clearReportId);
}

public class ReportRepository : IReportRepository
{
    private readonly SQLiteAsyncConnection _db;

    public ReportRepository(string dbPath)
    {
        _db = new SQLiteAsyncConnection(dbPath);
    }

    public async Task InitializeAsync()
    {
        await _db.CreateTableAsync<ClearReportEntity>();
        await _db.CreateTableAsync<AssignedUnitStatusEntity>();
    }

    public async Task<bool> ExistsByMessageIdAsync(string messageId)
    {
        var count = await _db.Table<ClearReportEntity>()
            .Where(x => x.MessageId == messageId)
            .CountAsync();
        return count > 0;
    }

    public async Task<int> InsertReportAsync(FireStationClearReport report)
    {
        var entity = new ClearReportEntity
        {
            MessageId = report.MessageId,
            ReceivedUtc = report.ReceivedUtc.UtcDateTime,
            Subject = report.Subject,
            RawBody = report.RawBody,
            IncidentNumber = report.IncidentNumber,
            Station = report.Station,
            Location = report.Location
        };

        await _db.InsertAsync(entity);

        foreach (var s in report.AssignedUnitStatuses)
        {
            await _db.InsertAsync(new AssignedUnitStatusEntity
            {
                ClearReportId = entity.Id,
                Unit = s.Unit,
                Group = s.Group,
                Status = s.Status,
                TimestampUtc = s.TimestampUtc.UtcDateTime
            });
        }

        return entity.Id;
    }

    public Task<List<AssignedUnitStatusEntity>> GetStatusesAsync(int clearReportId) =>
        _db.Table<AssignedUnitStatusEntity>()
           .Where(x => x.ClearReportId == clearReportId)
           .ToListAsync();
}
