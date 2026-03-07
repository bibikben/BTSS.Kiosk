using BTSS.IAR.Record.Models;
using System.Text.Json.Serialization;

namespace BTSS.IAR.Api.Models.Dtos;

/// <summary>
/// IAR incident payload (matches the JSON structure sent by the IAR system).
/// </summary>
public sealed class IarIncidentDto
{
    [JsonPropertyName("headers")]
    public IarHeadersDto? Headers { get; set; }

    [JsonPropertyName("details")]
    public IarDetailsDto? Details { get; set; }

    [JsonPropertyName("callers")]
    public List<IarCallerDto>? Callers { get; set; }

    [JsonPropertyName("units")]
    public List<IarUnitDto>? Units { get; set; }

    [JsonPropertyName("comments")]
    public List<IarCommentDto>? Comments { get; set; }

    /// <summary>
    /// Optional pre-rendered plain text representation.
    /// </summary>
    [JsonPropertyName("plainText")]
    public string? PlainText { get; set; }

    [JsonPropertyName("hash")]
    public string? Hash { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    /// <summary>
    /// Optional grouping produced upstream.
    /// </summary>
    [JsonPropertyName("groupedUnits")]
    public Dictionary<string, List<string>>? GroupedUnits { get; set; }
}

public sealed class IarHeadersDto
{
    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("locationName")]
    public string? LocationName { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }

    [JsonPropertyName("coordinates")]
    public string? Coordinates { get; set; }

    [JsonPropertyName("approximatedLocation")]
    public bool? ApproximatedLocation { get; set; }
}

public sealed class IarDetailsDto
{
    /// <summary>
    /// External incident identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("agency")]
    public string? Agency { get; set; }

    [JsonPropertyName("createdAtISO")]
    public DateTime? CreatedAtISO { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("closed")]
    public bool? Closed { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("dispatchedAt")]
    public DateTime? DispatchedAt { get; set; }

    [JsonPropertyName("updatedAtISO")]
    public DateTime? UpdatedAtISO { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }
}

public sealed class IarCallerDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }
}

public sealed class IarUnitDto
{
    /// <summary>
    /// Unit identifier (e.g., "Engine 1").
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Normalized unit status (e.g., "dispatched", "enroute", "onScene", "available").
    /// </summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("createdAtISO")]
    public DateTime? CreatedAtISO { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Original CAD status code (e.g., "dp", "er", "os", "av").
    /// </summary>
    [JsonPropertyName("statusOriginal")]
    public string? StatusOriginal { get; set; }
}

public sealed class IarCommentDto
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("createdAtISO")]
    public DateTime? CreatedAtISO { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }
}

/// <summary>
/// Typed request wrapper for IAR payload ingestion.
/// </summary>
public sealed class ReceiveIarCallDetailsRequest
{
    public int AgencyIdentifier { get; set; }

    public EmergencyCallUnified? Incident { get; set; } 
}
