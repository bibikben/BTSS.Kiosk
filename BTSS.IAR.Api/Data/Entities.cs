namespace BTSS.IAR.Api.Data;

public class CallRecordEntity
{
    public long Id { get; set; }

    public int AgencyId { get; set; }
    public string CallIdentifier { get; set; } = "";

    public string SourceSystem { get; set; } = ""; // IAR | EmailText | ...
    public short? SourceSystemId { get; set; }
    public SourceSystemEntity? SourceSystemLookup { get; set; }
    public string Payload { get; set; } = "";      // stored raw json/text

    public string? ParsedType { get; set; }
    public string? ParsedAddress { get; set; }
    public string? ParsedPriority { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public bool IsClosed { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public double? EstimatedMilesFromStation { get; set; }
    public int? PriorityId { get; set; }
    public PriorityEntity? Priority { get; set; }
    public int? CallTypeId { get; set; }
    public CallTypeEntity? CallType { get; set; }

    public int? CadAgencyId { get; set; }
    public CadAgencyEntity? CadAgency { get; set; }

    public int? StatusId { get; set; }
    public CallStatusEntity? Status { get; set; }

    public ICollection<CallUnitEntity> Units { get; set; } = new List<CallUnitEntity>();
}

public class CallUnitEntity
{
    public long Id { get; set; }
    public long CallRecordId { get; set; }
    public CallRecordEntity CallRecord { get; set; } = null!;

    public int UnitId { get; set; }
    public UnitEntity Unit { get; set; } = null!;

    public int StatusId { get; set; }
    public UnitStatusEntity Status { get; set; } = null!;

    public int? StatusOriginalId { get; set; }
    public UnitStatusEntity? StatusOriginal { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }
}

public class PollState
{
    public int Id { get; set; }
    public int AgencyId { get; set; }
    public DateTimeOffset LastCloseCheckUtc { get; set; } = DateTimeOffset.MinValue;
}

public class ApiClient
{
    public int Id { get; set; }
    public int AgencyId { get; set; }

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
}

public class Agency
{
    public int Id { get; set; }
    public string Name { get; set; } = "";

    public double? StationLatitude { get; set; }
    public double? StationLongitude { get; set; }
}
// -----------------------------
// Lookup tables
// -----------------------------

/// <summary>
/// Common contract for simple lookup tables that store a categorical string value.
/// </summary>
public interface IIntIdValueLookup
{
    int Id { get; set; }
    string Value { get; set; }
}

public enum SourceSystemCode : short
{
    IAR = 1,
    EmailText = 2
}

public class SourceSystemEntity
{
    public short Id { get; set; }
    public string Code { get; set; } = ""; // "IAR", "EmailText"
    public string? Description { get; set; }
}

public class PriorityEntity : IIntIdValueLookup
{
    public int Id { get; set; }
    public string Value { get; set; } = "";
}

public class CallTypeEntity : IIntIdValueLookup
{
    public int Id { get; set; }
    public string Value { get; set; } = "";
}

/// <summary>
/// IAR details.agency normalized table (separate from station/tenant Agency).
/// </summary>
public class CadAgencyEntity
{
    public int Id { get; set; }
    public string ExternalKey { get; set; } = ""; // value from IAR details.agency

    public string? Name { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Zip { get; set; }

    public string? PrimaryContactName { get; set; }
    public string? PrimaryContactPhone { get; set; }
    public string? PrimaryContactEmail { get; set; }
}

public class CallStatusEntity : IIntIdValueLookup
{
    public int Id { get; set; }
    public string Value { get; set; } = "";
}

public class UnitStatusEntity : IIntIdValueLookup
{
    public int Id { get; set; }
    public string Value { get; set; } = "";
}

/// <summary>
/// IAR units[].id normalized table (unit name/description can be enriched).
/// </summary>
public class UnitEntity
{
    public int Id { get; set; }
    public string ExternalKey { get; set; } = "";
    public string? Name { get; set; }
    public string? Description { get; set; }
}
