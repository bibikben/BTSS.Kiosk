using System.Text.Json;
using System.Text.Json.Nodes;

namespace BTSS.Installer;

internal static class ConfigWriters
{
    public static void WriteDisplayPreferences(InstallPlan plan)
    {
        var payload = new
        {
            apiBaseUrl = EnsureTrailingSlash(plan.ApiBaseUrl),
            clientId = plan.ClientId,
            clientSecret = plan.ClientSecret,
            scope = plan.Scope,
            serviceDatabasePath = Path.Combine(plan.CommonDataDir, "btss-service.db"),
            serviceConfigPath = plan.ServiceConfigPath,
            kioskConfigPath = plan.LocalKioskCompatPath,
            preferredDeviceId = plan.DeviceId,
            preferredClientNumericId = (int?)null,
            autoRefreshDashboard = true,
            dashboardRefreshSeconds = 30
        };

        Directory.CreateDirectory(Path.GetDirectoryName(plan.LocalDisplayPrefsPath)!);
        File.WriteAllText(plan.LocalDisplayPrefsPath, JsonSerializer.Serialize(payload, JsonDefaults.Options));
    }

    public static void WriteKioskCompatibility(InstallPlan plan)
    {
        var payload = new
        {
            apiBaseUrl = EnsureTrailingSlash(plan.ApiBaseUrl),
            apiClientId = plan.ClientId,
            apiClientSecret = plan.ClientSecret,
            displayName = plan.DisplayName,
            location = plan.Location,
            stationCode = plan.StationCode,
            stationName = plan.StationName,
            startupUrl = plan.StartupUrl,
            selectedMonitorIndex = plan.SelectedMonitorIndex
        };

        Directory.CreateDirectory(plan.LocalKioskCompatDir);
        File.WriteAllText(plan.LocalKioskCompatPath, JsonSerializer.Serialize(payload, JsonDefaults.Options));

        var installConfigDir = Path.Combine(plan.KioskInstallDir, "config");
        Directory.CreateDirectory(installConfigDir);
        File.WriteAllText(Path.Combine(installConfigDir, "bootstrap-compat.json"), JsonSerializer.Serialize(payload, JsonDefaults.Options));
    }

    public static void WriteServiceSettings(InstallPlan plan)
    {
        Directory.CreateDirectory(plan.ServiceInstallDir);
        Directory.CreateDirectory(plan.CommonDataDir);

        var payload = new
        {
            Logging = new
            {
                LogLevel = new
                {
                    Default = "Information",
                    Microsoft_Hosting_Lifetime = "Information"
                }
            },
            Service = new
            {
                ApiBaseUrl = EnsureTrailingSlash(plan.ApiBaseUrl),
                IncidentFeedPath = "/api/service/incidents",
                OAuthTokenPath = "/connect/token",
                ClientId = plan.ClientId,
                ClientSecret = plan.ClientSecret,
                Scope = "service.poll",
                PollIntervalSeconds = plan.PollIntervalSeconds,
                HttpTimeoutSeconds = 30,
                MaxConsecutiveFailuresBeforeBackoff = 3,
                MaxBackoffMinutes = 10,
                MaxPrintAttempts = 5,
                LocalDataDirectory = plan.CommonDataDir,
                DatabaseFileName = "btss-service.db",
                PrintOutputDirectory = "print-jobs",
                HealthLogDirectory = "health",
                PrinterName = string.IsNullOrWhiteSpace(plan.PrinterName) ? null : plan.PrinterName,
                EnableShellPrinting = plan.EnableShellPrinting
            }
        };

        var root = JsonSerializer.SerializeToNode(payload, JsonDefaults.Options)!.AsObject();
        if (root["Logging"] is JsonObject logging && logging["LogLevel"] is JsonObject level)
        {
            level["Microsoft.Hosting.Lifetime"] = level["Microsoft_Hosting_Lifetime"];
            level.Remove("Microsoft_Hosting_Lifetime");
        }

        File.WriteAllText(plan.ServiceConfigPath, root.ToJsonString(JsonDefaults.Options));
    }

    private static string EnsureTrailingSlash(string url) => url.Trim().TrimEnd('/') + "/";
}
