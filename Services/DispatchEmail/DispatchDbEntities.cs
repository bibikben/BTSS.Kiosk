using SQLite;

namespace BTSS.IAR.Kiosk.Services.DispatchEmail;

[Table("DispatchClearReports")]
public class DispatchClearReportEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public string MessageId { get; set; } = "";

    public DateTime ReceivedUtc { get; set; }
    public DateTime DispatchTime { get; set; }

    public string Subject { get; set; } = "";
    public string Agency { get; set; } = "";
    public string DispatchGroup { get; set; } = "";
    public string EventId { get; set; } = "";
    public string CaseNumber { get; set; } = "";
    public string EventTypeCode { get; set; } = "";
    public string EventTypeText { get; set; } = "";
    public string EventSubtypeCode { get; set; } = "";
    public string EventSubtypeText { get; set; } = "";
    public string Address { get; set; } = "";
    public string Municipality { get; set; } = "";
    public string CrossStreet { get; set; } = "";

    public string RawBody { get; set; } = "";
}

[Table("DispatchAssignedUnitStatuses")]
public class DispatchAssignedUnitStatusEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ClearReportId { get; set; }

    public string Unit { get; set; } = "";
    public string Group { get; set; } = "";
    public string Agency { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime TimestampUtc { get; set; }
}
