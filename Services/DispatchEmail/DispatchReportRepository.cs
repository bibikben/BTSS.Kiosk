using SQLite;

namespace BTSS.IAR.Kiosk.Services.DispatchEmail;

public interface IDispatchReportRepository
{
    Task InitializeAsync();
    Task<bool> ExistsByMessageIdAsync(string messageId);
    Task<int> InsertAsync(FireStationClearReport report);
    Task<List<DispatchAssignedUnitStatusEntity>> GetStatusesAsync(int reportId);
}

public class DispatchReportRepository : IDispatchReportRepository
{
    private SQLiteAsyncConnection? _db;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private static string DbPath => Path.Combine(FileSystem.AppDataDirectory, "dispatch.db3");

    public async Task InitializeAsync()
    {
        if (_db != null) return;
        await _initLock.WaitAsync();
        try
        {
            if (_db != null) return;
            _db = new SQLiteAsyncConnection(DbPath);
            await _db.CreateTableAsync<DispatchClearReportEntity>();
            await _db.CreateTableAsync<DispatchAssignedUnitStatusEntity>();
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<bool> ExistsByMessageIdAsync(string messageId)
    {
        await InitializeAsync();
        var c = await _db!.Table<DispatchClearReportEntity>().Where(x => x.MessageId == messageId).CountAsync();
        return c > 0;
    }

    public async Task<int> InsertAsync(FireStationClearReport report)
    {
        await InitializeAsync();

        var entity = new DispatchClearReportEntity
        {
            MessageId = report.MessageId,
            ReceivedUtc = report.ReceivedUtc.UtcDateTime,
            DispatchTime = report.DispatchTime,
            Subject = report.Subject,
            Agency = report.Agency,
            DispatchGroup = report.DispatchGroup,
            EventId = report.EventId,
            CaseNumber = report.CaseNumber,
            EventTypeCode = report.EventTypeCode,
            EventTypeText = report.EventTypeText,
            EventSubtypeCode = report.EventSubtypeCode,
            EventSubtypeText = report.EventSubtypeText,
            Address = report.Address,
            Municipality = report.Municipality,
            CrossStreet = report.CrossStreet,
            RawBody = report.RawBody
        };

        await _db!.InsertAsync(entity);

        foreach (var s in report.AssignedUnitStatuses)
        {
            await _db.InsertAsync(new DispatchAssignedUnitStatusEntity
            {
                ClearReportId = entity.Id,
                Unit = s.Unit,
                Group = s.Group,
                Agency = s.Agency,
                Status = s.Status,
                TimestampUtc = s.TimestampUtc.UtcDateTime
            });
        }

        return entity.Id;
    }

    public async Task<List<DispatchAssignedUnitStatusEntity>> GetStatusesAsync(int reportId)
    {
        await InitializeAsync();
        return await _db!.Table<DispatchAssignedUnitStatusEntity>()
            .Where(x => x.ClearReportId == reportId)
            .ToListAsync();
    }
}
