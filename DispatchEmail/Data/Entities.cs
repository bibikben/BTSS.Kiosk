using SQLite;

namespace BTSS.IAR.Kiosk.DispatchEmail.Data;

[Table("ClearReports")]
public class ClearReportEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Unique = true)]
    public string MessageId { get; set; } = "";

    public DateTime ReceivedUtc { get; set; }
    public string Subject { get; set; } = "";
    public string RawBody { get; set; } = "";

    public string? IncidentNumber { get; set; }
    public string? Station { get; set; }
    public string? Location { get; set; }
}

[Table("AssignedUnitStatuses")]
public class AssignedUnitStatusEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int ClearReportId { get; set; }

    public string Unit { get; set; } = "";
    public string Group { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime TimestampUtc { get; set; }
}
