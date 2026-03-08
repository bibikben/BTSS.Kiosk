using System.Text.Json;
using BTSS.IAR.Api.Data;

namespace BTSS.IAR.Web.Models;

public sealed record ReportCallListResponse(IncidentListItemDto[] Items, int AgencyId, int Count);
public sealed record PivotResponse(long IncidentId, int AgencyId, PivotRowDto[] Rows, int Count);
public sealed record DefinitionListItem(long Id, string Key, string Name, string Description);
