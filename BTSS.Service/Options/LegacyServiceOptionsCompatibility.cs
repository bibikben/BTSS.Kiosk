using Microsoft.Extensions.Configuration;

namespace BTSS.Service.Options;

public static class LegacyServiceOptionsCompatibility
{
    public static void Apply(IConfiguration configuration, ServiceRuntimeOptions options)
    {
        var iarApi = configuration.GetSection("IarApi");
        var polling = configuration.GetSection("Polling");
        var printing = configuration.GetSection("Printing");
        var storage = configuration.GetSection("Storage");
        var legacyService = configuration.GetSection("LegacyService");

        options.ApiBaseUrl = FirstNonEmpty(options.ApiBaseUrl, iarApi["BaseUrl"], legacyService["ApiBaseUrl"]) ?? options.ApiBaseUrl;
        options.IncidentFeedPath = FirstNonEmpty(options.IncidentFeedPath, iarApi["IncidentFeedPath"], legacyService["IncidentFeedPath"]) ?? options.IncidentFeedPath;
        options.OAuthTokenPath = FirstNonEmpty(options.OAuthTokenPath, iarApi["TokenPath"], iarApi["OAuthTokenPath"], legacyService["OAuthTokenPath"]) ?? options.OAuthTokenPath;
        options.ClientId = FirstNonEmpty(options.ClientId, iarApi["ClientId"], legacyService["ClientId"]);
        options.ClientSecret = FirstNonEmpty(options.ClientSecret, iarApi["ClientSecret"], legacyService["ClientSecret"]);
        options.Scope = FirstNonEmpty(options.Scope, iarApi["Scope"], legacyService["Scope"]) ?? options.Scope;
        options.PrinterName = FirstNonEmpty(options.PrinterName, printing["PrinterName"], legacyService["PrinterName"]);
        options.LocalDataDirectory = FirstNonEmpty(options.LocalDataDirectory, storage["LocalDataDirectory"], legacyService["LocalDataDirectory"]) ?? options.LocalDataDirectory;
        options.DatabaseFileName = FirstNonEmpty(options.DatabaseFileName, storage["DatabaseFileName"], legacyService["DatabaseFileName"]) ?? options.DatabaseFileName;
        options.PrintOutputDirectory = FirstNonEmpty(options.PrintOutputDirectory, storage["PrintOutputDirectory"], legacyService["PrintOutputDirectory"]) ?? options.PrintOutputDirectory;
        options.HealthLogDirectory = FirstNonEmpty(options.HealthLogDirectory, storage["HealthLogDirectory"], legacyService["HealthLogDirectory"]) ?? options.HealthLogDirectory;

        options.PollIntervalSeconds = FirstPositive(options.PollIntervalSeconds, polling["IntervalSeconds"], legacyService["PollIntervalSeconds"]);
        options.HttpTimeoutSeconds = FirstPositive(options.HttpTimeoutSeconds, polling["HttpTimeoutSeconds"], legacyService["HttpTimeoutSeconds"]);
        options.MaxConsecutiveFailuresBeforeBackoff = FirstPositive(options.MaxConsecutiveFailuresBeforeBackoff, polling["MaxConsecutiveFailuresBeforeBackoff"], legacyService["MaxConsecutiveFailuresBeforeBackoff"]);
        options.MaxBackoffMinutes = FirstPositive(options.MaxBackoffMinutes, polling["MaxBackoffMinutes"], legacyService["MaxBackoffMinutes"]);
        options.MaxPrintAttempts = FirstPositive(options.MaxPrintAttempts, printing["MaxPrintAttempts"], legacyService["MaxPrintAttempts"]);
        options.EnableShellPrinting = FirstBool(options.EnableShellPrinting, printing["EnableShellPrinting"], legacyService["EnableShellPrinting"]);
    }

    private static string? FirstNonEmpty(params string?[] values) => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    private static int FirstPositive(int current, params string?[] values)
    {
        foreach (var value in values)
        {
            if (int.TryParse(value, out var parsed) && parsed > 0)
                return parsed;
        }

        return current;
    }

    private static bool FirstBool(bool current, params string?[] values)
    {
        foreach (var value in values)
        {
            if (bool.TryParse(value, out var parsed))
                return parsed;
        }

        return current;
    }
}
