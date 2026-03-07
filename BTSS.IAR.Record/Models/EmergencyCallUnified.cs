// Auto-generated unified model combining the two observed IAR schemas.
// - Supports both Newtonsoft.Json and System.Text.Json
// - Uses a flexible DateTime converter that can read ISO strings or nulls.
//
// Source schemas:
//   1) "kiosk dispatch" style: { callers, comments, details, headers, groupedUnits, units, ... }
//   2) "IAR API" style:        { address, agencies, c_num, callers, comments, units, ... }

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;





namespace BTSS.IAR.Record.Models
{
    #region Converters

    /// <summary>
    /// Allows System.Text.Json to read either an ISO-8601 string, a native Date token, or null.
    /// Writes as ISO-8601 (round-trip "O") when value is present.
    /// </summary>
    public sealed class FlexibleDateTimeConverter : System.Text.Json.Serialization.JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                if (string.IsNullOrWhiteSpace(s)) return null;

                // Try DateTimeOffset first (more forgiving for Z / offsets), then DateTime.
                if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
                {
                    return dto.UtcDateTime;
                }

                if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
                {
                    return dt;
                }

                return null;
            }

            if (reader.TokenType == JsonTokenType.Number)
            {
                // Some APIs send unix ms/seconds; if encountered, try to interpret as unix seconds (heuristic).
                if (reader.TryGetInt64(out long n))
                {
                    // Heuristic: if it's too large, treat as milliseconds.
                    if (n > 10_000_000_000L)
                        return DateTimeOffset.FromUnixTimeMilliseconds(n).UtcDateTime;

                    if (n > 0)
                        return DateTimeOffset.FromUnixTimeSeconds(n).UtcDateTime;
                }
                return null;
            }

            if (reader.TokenType == JsonTokenType.StartObject)
            {
                // Some payloads might wrap dates; deserialize as JsonElement and try common fields.
                using var doc = JsonDocument.ParseValue(ref reader);
                if (doc.RootElement.TryGetProperty("date", out var dateEl) && dateEl.ValueKind == JsonValueKind.String)
                {
                    var s = dateEl.GetString();
                    if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
                        return dto.UtcDateTime;
                }
                return null;
            }

            // If it is already a Date token (rare in System.Text.Json), fallback to string parsing.
            try
            {
                var raw = reader.GetString();
                if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
                    return dto.UtcDateTime;
            }
            catch { /* ignore */ }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        }
    }

    #endregion

    #region Leaf Types (Unified)

    public record Agency(
        [property: JsonProperty("agency")]
        [property: JsonPropertyName("agency")] string? Name,

        [property: JsonProperty("caseNumbers")]
        [property: JsonPropertyName("caseNumbers")] IReadOnlyList<string>? CaseNumbers,

        [property: JsonProperty("units")]
        [property: JsonPropertyName("units")] IReadOnlyList<string>? Units,

        [property: JsonProperty("dispatchGroup")]
        [property: JsonPropertyName("dispatchGroup")] string? DispatchGroup,

        [property: JsonProperty("priority")]
        [property: JsonPropertyName("priority")] string? Priority,

        [property: JsonProperty("type")]
        [property: JsonPropertyName("type")] string? Type,

        [property: JsonProperty("typeCode")]
        [property: JsonPropertyName("typeCode")] string? TypeCode,

        [property: JsonProperty("subtype")]
        [property: JsonPropertyName("subtype")] string? Subtype,

        [property: JsonProperty("subtypeCode")]
        [property: JsonPropertyName("subtypeCode")] string? SubtypeCode,

        [property: JsonProperty("closed")]
        [property: JsonPropertyName("closed")] bool? Closed,

        // Metadata fields (schema 2)
        [property: JsonProperty("createdAgency")]
        [property: JsonPropertyName("createdAgency")] string? CreatedAgency,

        [property: JsonProperty("createdBy")]
        [property: JsonPropertyName("createdBy")] string? CreatedBy,

        [property: JsonProperty("updatedAgency")]
        [property: JsonPropertyName("updatedAgency")] string? UpdatedAgency,

        [property: JsonProperty("updatedBy")]
        [property: JsonPropertyName("updatedBy")] string? UpdatedBy,

        [property: JsonProperty("id")]
        [property: JsonPropertyName("id")] string? Id,

        [property: JsonProperty("createdAt")]
        [property: JsonPropertyName("createdAt")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? CreatedAt,

        [property: JsonProperty("updatedAt")]
        [property: JsonPropertyName("updatedAt")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? UpdatedAt,

        [property: JsonProperty("dispatchedAt")]
        [property: JsonPropertyName("dispatchedAt")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? DispatchedAt
    );

    public record Caller(
        // Common fields (both schemas)
        [property: JsonProperty("name")]
        [property: JsonPropertyName("name")] string? Name,

        [property: JsonProperty("phoneNumber")]
        [property: JsonPropertyName("phoneNumber")] string? PhoneNumber,

        // Extra fields (schema 2)
        [property: JsonProperty("firstCall")]
        [property: JsonPropertyName("firstCall")] bool? FirstCall,

        [property: JsonProperty("locationVerified")]
        [property: JsonPropertyName("locationVerified")] bool? LocationVerified,

        [property: JsonProperty("latitude")]
        [property: JsonPropertyName("latitude")] string? LatitudeRaw,

        [property: JsonProperty("longitude")]
        [property: JsonPropertyName("longitude")] string? LongitudeRaw,

        [property: JsonProperty("address")]
        [property: JsonPropertyName("address")] JsonElement? Address,

        [property: JsonProperty("city")]
        [property: JsonPropertyName("city")] JsonElement? City,

        [property: JsonProperty("createdAgency")]
        [property: JsonPropertyName("createdAgency")] string? CreatedAgency,

        [property: JsonProperty("createdBy")]
        [property: JsonPropertyName("createdBy")] string? CreatedBy,

        [property: JsonProperty("createdAt")]
        [property: JsonPropertyName("createdAt")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? CreatedAt,

        [property: JsonProperty("updatedAgency")]
        [property: JsonPropertyName("updatedAgency")] JsonElement? UpdatedAgency,

        [property: JsonProperty("updatedBy")]
        [property: JsonPropertyName("updatedBy")] JsonElement? UpdatedBy,

        [property: JsonProperty("updatedAt")]
        [property: JsonPropertyName("updatedAt")] JsonElement? UpdatedAt
    );

    public record Comment(
        // Common field
        [property: JsonProperty("message")]
        [property: JsonPropertyName("message")] string? Message,

        // Schema 1 fields
        [property: JsonProperty("createdAt")]
        [property: JsonPropertyName("createdAt")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? CreatedAt,

        [property: JsonProperty("createdAtISO")]
        [property: JsonPropertyName("createdAtISO")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? CreatedAtISO,

        // Schema 2 fields
        [property: JsonProperty("createdAgency")]
        [property: JsonPropertyName("createdAgency")] string? CreatedAgency,

        [property: JsonProperty("createdBy")]
        [property: JsonPropertyName("createdBy")] string? CreatedBy
    );

    public record Unit(
        // Common identifiers / status
        [property: JsonProperty("id")]
        [property: JsonPropertyName("id")] string? Id,

        [property: JsonProperty("status")]
        [property: JsonPropertyName("status")] string? Status,

        // Schema 1
        [property: JsonProperty("statusOriginal")]
        [property: JsonPropertyName("statusOriginal")] string? StatusOriginal,

        [property: JsonProperty("createdAt")]
        [property: JsonPropertyName("createdAt")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? CreatedAt,

        [property: JsonProperty("createdAtISO")]
        [property: JsonPropertyName("createdAtISO")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? CreatedAtISO,

        // Schema 2 fields
        [property: JsonProperty("agency")]
        [property: JsonPropertyName("agency")] string? Agency,

        [property: JsonProperty("agencyEventId")]
        [property: JsonPropertyName("agencyEventId")] string? AgencyEventId,

        [property: JsonProperty("type")]
        [property: JsonPropertyName("type")] string? Type,

        [property: JsonProperty("station")]
        [property: JsonPropertyName("station")] string? Station,

        [property: JsonProperty("quarters")]
        [property: JsonPropertyName("quarters")] string? Quarters,

        [property: JsonProperty("dispatched")]
        [property: JsonPropertyName("dispatched")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? Dispatched,

        [property: JsonProperty("enroute")]
        [property: JsonPropertyName("enroute")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? Enroute,

        [property: JsonProperty("arrived")]
        [property: JsonPropertyName("arrived")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? Arrived,

        [property: JsonProperty("cleared")]
        [property: JsonPropertyName("cleared")] JsonElement? Cleared,

        [property: JsonProperty("created")]
        [property: JsonPropertyName("created")] JsonElement? Created,

        [property: JsonProperty("transportBegin")]
        [property: JsonPropertyName("transportBegin")] JsonElement? TransportBegin,

        [property: JsonProperty("transportComplete")]
        [property: JsonPropertyName("transportComplete")] JsonElement? TransportComplete,

        [property: JsonProperty("unitNotes")]
        [property: JsonPropertyName("unitNotes")] JsonElement? UnitNotes,

        [property: JsonProperty("createdAgency")]
        [property: JsonPropertyName("createdAgency")] string? CreatedAgency,

        [property: JsonProperty("createdBy")]
        [property: JsonPropertyName("createdBy")] string? CreatedBy
    );

    #endregion

    #region Schema-1 Structured Types (kept, but optional in unified payload)

    public record Details(
        [property: JsonProperty("agency")]
        [property: JsonPropertyName("agency")] string? Agency,

        [property: JsonProperty("closed")]
        [property: JsonPropertyName("closed")] bool? Closed,

        [property: JsonProperty("id")]
        [property: JsonPropertyName("id")] string? Id,

        [property: JsonProperty("source")]
        [property: JsonPropertyName("source")] string? Source,

        [property: JsonProperty("status")]
        [property: JsonPropertyName("status")] string? Status,

        [property: JsonProperty("createdAt")]
        [property: JsonPropertyName("createdAt")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? CreatedAt,

        [property: JsonProperty("createdAtISO")]
        [property: JsonPropertyName("createdAtISO")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? CreatedAtISO,

        [property: JsonProperty("updatedAt")]
        [property: JsonPropertyName("updatedAt")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? UpdatedAt,

        [property: JsonProperty("updatedAtISO")]
        [property: JsonPropertyName("updatedAtISO")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? UpdatedAtISO,

        [property: JsonProperty("dispatchedAt")]
        [property: JsonPropertyName("dispatchedAt")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? DispatchedAt
    );

    public record GroupedUnits(
        [property: JsonProperty("available")]
        [property: JsonPropertyName("available")] IReadOnlyList<string>? Available,

        [property: JsonProperty("dispatched")]
        [property: JsonPropertyName("dispatched")] IReadOnlyList<string>? Dispatched
    );

    public record Headers(
        [property: JsonProperty("address")]
        [property: JsonPropertyName("address")] string? Address,

        [property: JsonProperty("locationName")]
        [property: JsonPropertyName("locationName")] string? LocationName,

        [property: JsonProperty("coordinates")]
        [property: JsonPropertyName("coordinates")] string? Coordinates,

        [property: JsonProperty("latitude")]
        [property: JsonPropertyName("latitude")] double? Latitude,

        [property: JsonProperty("longitude")]
        [property: JsonPropertyName("longitude")] double? Longitude,

        [property: JsonProperty("approximatedLocation")]
        [property: JsonPropertyName("approximatedLocation")] bool? ApproximatedLocation,

        [property: JsonProperty("priority")]
        [property: JsonPropertyName("priority")] string? Priority,

        [property: JsonProperty("type")]
        [property: JsonPropertyName("type")] string? Type
    );

    #endregion

    #region Unified Root

    /// <summary>
    /// Unified root model that can deserialize BOTH observed payload shapes.
    /// Any field not present in a given payload will remain null/default.
    /// </summary>
    public record EmergencyCallUnified(
        // ----------------------
        // Common / overlaps
        // ----------------------

        [property: JsonProperty("callers")]
        [property: JsonPropertyName("callers")] IReadOnlyList<Caller>? Callers,

        [property: JsonProperty("comments")]
        [property: JsonPropertyName("comments")] IReadOnlyList<Comment>? Comments,

        [property: JsonProperty("units")]
        [property: JsonPropertyName("units")] IReadOnlyList<Unit>? Units,

        // ----------------------
        // Schema 1 ("dispatch" style)
        // ----------------------

        [property: JsonProperty("details")]
        [property: JsonPropertyName("details")] Details? Details,

        [property: JsonProperty("headers")]
        [property: JsonPropertyName("headers")] Headers? Headers,

        [property: JsonProperty("groupedUnits")]
        [property: JsonPropertyName("groupedUnits")] GroupedUnits? GroupedUnits,

        [property: JsonProperty("filePath")]
        [property: JsonPropertyName("filePath")] string? FilePath,

        [property: JsonProperty("hash")]
        [property: JsonPropertyName("hash")] string? Hash,

        [property: JsonProperty("plainText")]
        [property: JsonPropertyName("plainText")] string? PlainText,

        [property: JsonProperty("timestamp")]
        [property: JsonPropertyName("timestamp")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? Timestamp,

        // ----------------------
        // Schema 2 ("IAR API" style)
        // ----------------------

        [property: JsonProperty("id")]
        [property: JsonPropertyName("id")] string? Id,

        [property: JsonProperty("c_num")]
        [property: JsonPropertyName("c_num")] string? CNum,

        [property: JsonProperty("address")]
        [property: JsonPropertyName("address")] string? Address,

        [property: JsonProperty("crossStreet")]
        [property: JsonPropertyName("crossStreet")] string? CrossStreet,

        [property: JsonProperty("municipality")]
        [property: JsonPropertyName("municipality")] string? Municipality,

        [property: JsonProperty("zipCode")]
        [property: JsonPropertyName("zipCode")] string? ZipCode,

        [property: JsonProperty("latitude")]
        [property: JsonPropertyName("latitude")] string? LatitudeRaw,

        [property: JsonProperty("longitude")]
        [property: JsonPropertyName("longitude")] string? LongitudeRaw,

        [property: JsonProperty("num1")]
        [property: JsonPropertyName("num1")] string? Num1,

        [property: JsonProperty("stations")]
        [property: JsonPropertyName("stations")] IReadOnlyList<string>? Stations,

        [property: JsonProperty("channels")]
        [property: JsonPropertyName("channels")] IReadOnlyList<JsonElement>? Channels,

        [property: JsonProperty("locationInfo")]
        [property: JsonPropertyName("locationInfo")] JsonElement? LocationInfo,

        [property: JsonProperty("agencies")]
        [property: JsonPropertyName("agencies")] IReadOnlyList<Agency>? Agencies,

        // Metadata fields
        [property: JsonProperty("createdAgency")]
        [property: JsonPropertyName("createdAgency")] string? CreatedAgency,

        [property: JsonProperty("createdBy")]
        [property: JsonPropertyName("createdBy")] string? CreatedBy,

        [property: JsonProperty("updatedAgency")]
        [property: JsonPropertyName("updatedAgency")] string? UpdatedAgency,

        [property: JsonProperty("updatedBy")]
        [property: JsonPropertyName("updatedBy")] string? UpdatedBy,

        [property: JsonProperty("createdAt")]
        [property: JsonPropertyName("createdAt")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? CreatedAt,

        [property: JsonProperty("updatedAt")]
        [property: JsonPropertyName("updatedAt")]
        [property: System.Text.Json.Serialization.JsonConverter(typeof(FlexibleDateTimeConverter))] DateTime? UpdatedAt
    );

    #endregion
}
