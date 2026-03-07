namespace BTSS.IAR.Models;

public sealed class ReportFilterOptions
{
    public string SearchText { get; set; } = string.Empty;
    public string Agency { get; set; } = string.Empty;
    public string CallType { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public sealed class ReportSummaryRow
{
    public string IncidentId { get; set; } = string.Empty;
    public string Agency { get; set; } = string.Empty;
    public string CallType { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public DateTimeOffset? FirstSeenUtc { get; set; }
    public DateTimeOffset? LastSeenUtc { get; set; }
    public string PrimaryCaller { get; set; } = string.Empty;
    public string UnitSummary { get; set; } = string.Empty;
    public int CommentCount { get; set; }
    public int TransitionCount { get; set; }
    public int MinutesOpen { get; set; }
    public string SummaryText { get; set; } = string.Empty;
}

public sealed class ReportDetailModel
{
    public string IncidentId { get; set; } = string.Empty;
    public string Agency { get; set; } = string.Empty;
    public string CallType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Coordinates { get; set; } = string.Empty;
    public string CallerSummary { get; set; } = string.Empty;
    public DateTimeOffset? FirstSeenUtc { get; set; }
    public DateTimeOffset? LastSeenUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public List<IncidentTransitionItem> Timeline { get; set; } = new();
    public List<IncidentUnitItem> Units { get; set; } = new();
    public List<IncidentCommentItem> Comments { get; set; } = new();
}

public sealed class MonthlyReportRow
{
    public string MonthKey { get; set; } = string.Empty;
    public string Agency { get; set; } = string.Empty;
    public string CallType { get; set; } = string.Empty;
    public int IncidentCount { get; set; }
    public int TotalMinutesOpen { get; set; }
    public int AverageMinutesOpen { get; set; }
    public int PrintedCount { get; set; }
    public int FailedPrintCount { get; set; }
}

public sealed class ReportsSnapshot
{
    public List<ReportSummaryRow> SummaryRows { get; set; } = new();
    public List<MonthlyReportRow> MonthlyRows { get; set; } = new();
    public Dictionary<string, int> TotalsByAgency { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> TotalsByCallType { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTimeOffset? GeneratedAtUtc { get; set; }
}
