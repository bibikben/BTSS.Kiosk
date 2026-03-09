using System.Text.Json;

namespace BTSS.IAR.Api.Data;

public sealed record ReportingSubjectAreaDto(string Key, string Name, string Description, bool DiagnosticsOnly, ReportingFieldDto[] Fields, ReportingFilterOperatorDto[] SupportedOperators);
public sealed record ReportingFieldDto(string Key, string Label, string DataType, bool IsDefault, bool Filterable, bool Sortable);
public sealed record ReportingFilterOperatorDto(string Key, string Label, string DataType);
public sealed record ReportingModelDto(int AgencyId, string AgencyName, string[] AllowedActions, ReportingSubjectAreaDto[] SubjectAreas);
public sealed record ReportingFilterDto(string Field, string Operator, string? Value);
public sealed record ReportingSortDto(string Field, bool Descending);
public sealed record ReportingQueryRequestDto(string SubjectArea, string[]? Fields, ReportingFilterDto[]? Filters, ReportingSortDto[]? Sort, int? Skip, int? Take);
public sealed record ReportingQueryRowDto(Dictionary<string, object?> Values);
public sealed record ReportingQueryResultDto(string SubjectArea, int AgencyId, string[] Fields, int TotalCount, int ReturnedCount, ReportingQueryRowDto[] Rows, string[] AppliedSecurityFilters);
public sealed record ReportingSavedReportDto(long Id, int AgencyId, string Name, string SubjectArea, string Description, bool IsShared, bool IsSystem, DateTime UpdatedAtUtc, JsonElement Definition);
public sealed record ReportingSaveReportRequestDto(long? Id, string Name, string SubjectArea, string? Description, bool IsShared, JsonElement Definition);
public sealed record ReportingExecuteReportRequestDto(long? SavedReportId, ReportingQueryRequestDto? Query);
