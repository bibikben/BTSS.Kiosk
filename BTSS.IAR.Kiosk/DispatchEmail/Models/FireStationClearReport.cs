namespace BTSS.IAR.Kiosk.DispatchEmail.Models;

public class FireStationClearReport
{
    public string MessageId { get; set; } = "";
    public DateTime ReceivedUtc { get; set; }
    public string Subject { get; set; } = "";
    public string RawBody { get; set; } = "";

    public string? IncidentNumber { get; set; }
    public string? Station { get; set; }
    public string? Location { get; set; }

    public List<AssignedUnitStatus> AssignedUnitStatuses { get; set; } = new();
}

public class AssignedUnitStatus
{
    public string Unit { get; set; } = "";
    public string Group { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime TimestampUtc { get; set; }
}
