using System.Text.Json;

namespace BTSS.IAR.Api.Data;

public sealed class StatusNormalizationRuleEntity
{
    public long Id { get; set; }
    public int? AgencyId { get; set; }
    public Agency? Agency { get; set; }
    public string RawCode { get; set; } = string.Empty;
    public string NormalizedCode { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SavedReportEntity
{
    public long Id { get; set; }
    public int AgencyId { get; set; }
    public Agency? Agency { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public string ParametersJson { get; set; } = "{}";
    public int? CreatedByUserId { get; set; }
    public UserAccount? CreatedByUser { get; set; }
    public bool IsShared { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ReportDefinitionEntity
{
    public long Id { get; set; }
    public int? AgencyId { get; set; }
    public Agency? Agency { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefaultParametersJson { get; set; } = "{}";
    public bool IsSystem { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ReportExecutionEntity
{
    public long Id { get; set; }
    public int AgencyId { get; set; }
    public Agency? Agency { get; set; }
    public long? SavedReportId { get; set; }
    public SavedReportEntity? SavedReport { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string ParametersJson { get; set; } = "{}";
    public string ResultSummaryJson { get; set; } = "{}";
    public int? ExecutedByUserId { get; set; }
    public UserAccount? ExecutedByUser { get; set; }
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ExportJobEntity
{
    public long Id { get; set; }
    public int AgencyId { get; set; }
    public Agency? Agency { get; set; }
    public long? ReportExecutionId { get; set; }
    public ReportExecutionEntity? ReportExecution { get; set; }
    public string Format { get; set; } = "csv";
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/csv";
    public string PayloadText { get; set; } = string.Empty;
    public int? RequestedByUserId { get; set; }
    public UserAccount? RequestedByUser { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed record StatusNormalizationRuleDto(long Id, int? AgencyId, string RawCode, string NormalizedCode, int SortOrder, bool IsEnabled, DateTime UpdatedAtUtc);
public sealed record ReportDefinitionDto(long Id, int? AgencyId, string Key, string Name, string Description, JsonElement DefaultParameters, bool IsSystem, bool IsEnabled, DateTime UpdatedAtUtc);
public sealed record SavedReportDto(long Id, int AgencyId, string Name, string ReportType, JsonElement Parameters, int? CreatedByUserId, bool IsShared, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);
public sealed record ReportExecutionDto(long Id, int AgencyId, long? SavedReportId, string ReportType, JsonElement Parameters, JsonElement ResultSummary, int? ExecutedByUserId, DateTime ExecutedAtUtc);
public sealed record ExportJobDto(long Id, int AgencyId, long? ReportExecutionId, string Format, string FileName, string ContentType, DateTime CreatedAtUtc);
public sealed record PivotRowDto(int? AgencyId, string UnitIdentifier, DateTime? DispatchedAtUtc, DateTime? EnrouteAtUtc, DateTime? ArrivedAtUtc, DateTime? TransportBeginAtUtc, DateTime? TransportCompleteAtUtc, DateTime? ClearedAtUtc, DateTime? InQuartersAtUtc, double? DispatchToEnrouteMinutes, double? EnrouteToArrivalMinutes, double? SceneToClearMinutes, double? DispatchToClearMinutes);
public sealed record CallReportDto(IncidentDetailDto Incident, IncidentAgencySummaryDto[] Agencies, IncidentCallerDto[] Callers, IncidentCommentDto[] Comments, IncidentUnitDto[] Units, UnitStatusEventDto[] TimelineEvents, PivotRowDto[] PivotRows, string PrintableHtml);
public sealed record SaveReportRequest(string Name, string ReportType, JsonElement Parameters, bool IsShared);
public sealed record RunReportRequest(string ReportType, long? SavedReportId, JsonElement Parameters);
public sealed record ExportReportRequest(string ReportType, long? IncidentId, long? ExecutionId, string? Format, JsonElement Parameters);
