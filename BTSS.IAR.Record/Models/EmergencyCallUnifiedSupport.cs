using System.Globalization;
using System.Text.Json;

namespace BTSS.IAR.Record.Models;

public static class EmergencyCallUnifiedJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    static EmergencyCallUnifiedJson()
    {
        Options.Converters.Add(new FlexibleDateTimeConverter());
    }

    public static EmergencyCallUnified? Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<EmergencyCallUnified>(json, Options);
    }

    public static string Serialize(EmergencyCallUnified call)
        => JsonSerializer.Serialize(call, Options);
}

public static class EmergencyCallUnifiedExtensions
{
    public static string? GetCallIdentifier(this EmergencyCallUnified? call)
        => call?.Details?.Id ?? call?.Id ?? call?.Num1 ?? call?.CNum;

    public static string? GetAgencyName(this EmergencyCallUnified? call)
        => call?.Details?.Agency
            ?? call?.Agencies?.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Name))?.Name
            ?? call?.CreatedAgency;

    public static string? GetAddress(this EmergencyCallUnified? call)
        => call?.Headers?.Address ?? call?.Address;

    public static string? GetLocationName(this EmergencyCallUnified? call)
        => call?.Headers?.LocationName ?? call?.LocationInfo?.ToString();

    public static string? GetCallType(this EmergencyCallUnified? call)
        => call?.Headers?.Type
            ?? call?.Agencies?.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Type))?.Type;

    public static string? GetPriority(this EmergencyCallUnified? call)
        => call?.Headers?.Priority
            ?? call?.Agencies?.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Priority))?.Priority;

    public static bool GetIsClosed(this EmergencyCallUnified? call)
        => call?.Details?.Closed
            ?? call?.Agencies?.Any(a => a.Closed == true)
            ?? false;

    public static DateTime? GetUpdatedAtUtc(this EmergencyCallUnified? call)
        => call?.Details?.UpdatedAt
            ?? call?.Details?.UpdatedAtISO
            ?? call?.UpdatedAt
            ?? call?.Timestamp
            ?? call?.Agencies?.Select(a => a.UpdatedAt).Where(d => d != null).Max();

    public static DateTime? GetCreatedAtUtc(this EmergencyCallUnified? call)
        => call?.Details?.CreatedAt
            ?? call?.Details?.CreatedAtISO
            ?? call?.CreatedAt
            ?? call?.Agencies?.Select(a => a.CreatedAt).Where(d => d != null).Min();

    public static double? GetLatitude(this EmergencyCallUnified? call)
    {
        if (call?.Headers?.Latitude is double lat) return lat;
        if (double.TryParse(call?.LatitudeRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return null;
    }

    public static double? GetLongitude(this EmergencyCallUnified? call)
    {
        if (call?.Headers?.Longitude is double lng) return lng;
        if (double.TryParse(call?.LongitudeRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return null;
    }

    public static string NormalizeUnitStatus(this Unit? unit)
    {
        var value = (unit?.StatusOriginal ?? unit?.Status ?? string.Empty).Trim().ToUpperInvariant();
        return value switch
        {
            "DISPATCHED" => "DP",
            "ENROUTE" => "ER",
            "ONSCENE" => "OS",
            "AVAILABLE" => "AV",
            "TRANSPORT" => "TR",
            "TRANSPORTBEGIN" => "TR",
            "TRANSPORTCOMPLETE" => "TC",
            "ATMEDICAL" => "AM",
            _ => value
        };
    }

    public static DateTime? GetBestTimestampUtc(this Unit? unit)
        => unit?.CreatedAt
            ?? unit?.CreatedAtISO
            ?? unit?.Arrived
            ?? unit?.Enroute
            ?? unit?.Dispatched;
}
