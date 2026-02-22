using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace BTSS.IAR.Record.Models;

/// <summary>
/// Shared IAR call record model.
///
/// Notes:
/// - The upstream payload shape appears consistent between "open" and "close" events.
/// - Some properties (e.g. locationName, groupedUnits.dispatched/available) are optional.
/// - Date fields are represented as DateTimeOffset? to preserve the timezone offset coming from IAR.
/// </summary>
public record IarCallRecord(
    [property: JsonProperty("headers", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("headers")] IarHeaders? Headers,

    [property: JsonProperty("details", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("details")] IarDetails? Details,

    [property: JsonProperty("callers", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("callers")] IReadOnlyList<IarCaller>? Callers,

    [property: JsonProperty("units", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("units")] IReadOnlyList<IarUnit>? Units,

    [property: JsonProperty("comments", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("comments")] IReadOnlyList<IarComment>? Comments,

    [property: JsonProperty("plainText", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("plainText")] string? PlainText,

    [property: JsonProperty("hash", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("hash")] string? Hash,

    [property: JsonProperty("timestamp", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("timestamp")] DateTimeOffset? Timestamp,

    [property: JsonProperty("filePath", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("filePath")] string? FilePath,

    [property: JsonProperty("groupedUnits", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("groupedUnits")] IarGroupedUnits? GroupedUnits
);

public record IarHeaders(
    [property: JsonProperty("address", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("address")] string? Address,

    [property: JsonProperty("locationName", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("locationName")] string? LocationName,

    [property: JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("type")] string? Type,

    [property: JsonProperty("priority", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("priority")] string? Priority,

    [property: JsonProperty("latitude", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("latitude")] double? Latitude,

    [property: JsonProperty("longitude", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("longitude")] double? Longitude,

    [property: JsonProperty("coordinates", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("coordinates")] string? Coordinates,

    [property: JsonProperty("approximatedLocation", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("approximatedLocation")] bool? ApproximatedLocation
);

public record IarDetails(
    [property: JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("id")] string? Id,

    [property: JsonProperty("agency", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("agency")] string? Agency,

    [property: JsonProperty("createdAtISO", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("createdAtISO")] DateTimeOffset? CreatedAtISO,

    [property: JsonProperty("createdAt", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("createdAt")] DateTimeOffset? CreatedAt,

    [property: JsonProperty("closed", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("closed")] bool? Closed,

    [property: JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("status")] string? Status,

    [property: JsonProperty("source", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("source")] string? Source,

    [property: JsonProperty("dispatchedAt", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("dispatchedAt")] DateTimeOffset? DispatchedAt,

    [property: JsonProperty("updatedAtISO", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("updatedAtISO")] DateTimeOffset? UpdatedAtISO,

    [property: JsonProperty("updatedAt", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("updatedAt")] DateTimeOffset? UpdatedAt
);

public record IarCaller(
    [property: JsonProperty("name", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("name")] string? Name,

    [property: JsonProperty("phoneNumber", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("phoneNumber")] string? PhoneNumber
);

public record IarUnit(
    [property: JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("id")] string? Id,

    [property: JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("status")] string? Status,

    [property: JsonProperty("createdAtISO", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("createdAtISO")] DateTimeOffset? CreatedAtISO,

    [property: JsonProperty("createdAt", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("createdAt")] DateTimeOffset? CreatedAt,

    [property: JsonProperty("statusOriginal", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("statusOriginal")] string? StatusOriginal
);

public record IarComment(
    [property: JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("message")] string? Message,

    [property: JsonProperty("createdAtISO", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("createdAtISO")] DateTimeOffset? CreatedAtISO,

    [property: JsonProperty("createdAt", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("createdAt")] DateTimeOffset? CreatedAt
);

public record IarGroupedUnits(
    [property: JsonProperty("dispatched", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("dispatched")] IReadOnlyList<string>? Dispatched,

    [property: JsonProperty("available", NullValueHandling = NullValueHandling.Ignore)]
    [property: JsonPropertyName("available")] IReadOnlyList<string>? Available
);
