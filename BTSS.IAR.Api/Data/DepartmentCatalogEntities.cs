
using System.ComponentModel.DataAnnotations;

namespace BTSS.IAR.Api.Data;

public sealed class DepartmentEntity
{
    public int Id { get; set; }
    [MaxLength(4)] public string DepartmentCode { get; set; } = string.Empty;
    [MaxLength(256)] public string DepartmentName { get; set; } = string.Empty;
    [MaxLength(512)] public string? MainAddress { get; set; }
    [MaxLength(128)] public string? City { get; set; }
    [MaxLength(64)] public string? State { get; set; } = "NY";
    [MaxLength(32)] public string? PostalCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class StationEntity
{
    public int Id { get; set; }
    public int DepartmentId { get; set; }
    public DepartmentEntity? Department { get; set; }
    [MaxLength(20)] public string StationNumber { get; set; } = string.Empty;
    [MaxLength(64)] public string? StationCode { get; set; }
    [MaxLength(512)] public string Address { get; set; } = string.Empty;
    [MaxLength(128)] public string? City { get; set; }
    [MaxLength(64)] public string? State { get; set; } = "NY";
    [MaxLength(32)] public string? PostalCode { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class UnitCatalogEntity
{
    public int Id { get; set; }
    [MaxLength(30)] public string Code { get; set; } = string.Empty;
    [MaxLength(30)] public string? TransmitCode { get; set; }
    [MaxLength(128)] public string? EquipmentName { get; set; }
    public int DepartmentId { get; set; }
    public DepartmentEntity? Department { get; set; }
    public int? StationId { get; set; }
    public StationEntity? Station { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ApiClientDepartmentEntity
{
    public long Id { get; set; }
    public int ApiClientId { get; set; }
    public ApiClient? ApiClient { get; set; }
    public int DepartmentId { get; set; }
    public DepartmentEntity? Department { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class IncidentCaseNumberEntity
{
    public long Id { get; set; }
    public long IncidentId { get; set; }
    public IncidentEntity? Incident { get; set; }
    [MaxLength(30)] public string CaseNumber { get; set; } = string.Empty;
    [MaxLength(8)] public string AgencyPrefix { get; set; } = string.Empty;
    [MaxLength(30)] public string? Source { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
