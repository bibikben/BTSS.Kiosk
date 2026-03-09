using System.ComponentModel.DataAnnotations;

namespace BTSS.Service.Options;

public sealed class ServiceRuntimeOptions
{
    public const string SectionName = "Service";

    [Required]
    public string ApiBaseUrl { get; set; } = "https://localhost:56800/";

    [Required]
    public string IncidentFeedPath { get; set; } = "/api/service/incidents";

    public string DeviceRegisterPath { get; set; } = "/api/device/register";

    public string DeviceHeartbeatPath { get; set; } = "/api/device/heartbeat";

    public string SyncBootstrapPath { get; set; } = "/api/sync/bootstrap";

    public string SyncChangesPath { get; set; } = "/api/sync/changes";

    public string SyncAckPath { get; set; } = "/api/sync/ack";

    public string? OAuthTokenPath { get; set; } = "/connect/token";

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public string Scope { get; set; } = "service.poll";

    [Range(15, 3600)]
    public int PollIntervalSeconds { get; set; } = 60;

    [Range(15, 300)]
    public int HttpTimeoutSeconds { get; set; } = 30;

    [Range(1, 10)]
    public int MaxConsecutiveFailuresBeforeBackoff { get; set; } = 3;

    [Range(1, 60)]
    public int MaxBackoffMinutes { get; set; } = 10;

    [Range(1, 10)]
    public int MaxPrintAttempts { get; set; } = 5;
    public string LocalDataDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BTSS", "Service");

    public string DatabaseFileName { get; set; } = "btss-service.db";

    public string PrintOutputDirectory { get; set; } = "print-jobs";

    public string HealthLogDirectory { get; set; } = "health";

    public string? PrinterName { get; set; }

    public bool EnableShellPrinting { get; set; }

    public string? DeviceId { get; set; }

    public string? DeviceName { get; set; }

    public string MachineName { get; set; } = Environment.MachineName;

    public string DeviceType { get; set; } = "service";

    public string ResolveDatabasePath() => Path.Combine(LocalDataDirectory, DatabaseFileName);

    public string ResolvePrintOutputDirectory() => Path.Combine(LocalDataDirectory, PrintOutputDirectory);

    public string ResolveHealthLogDirectory() => Path.Combine(LocalDataDirectory, HealthLogDirectory);

    public string ResolveDeviceId()
        => string.IsNullOrWhiteSpace(DeviceId) ? MachineName : DeviceId!;

    public bool Validate(out string error)
    {
        if (string.IsNullOrWhiteSpace(ApiBaseUrl))
        {
            error = "ApiBaseUrl is required.";
            return false;
        }

        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out _))
        {
            error = "ApiBaseUrl must be an absolute URI.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(IncidentFeedPath))
        {
            error = "IncidentFeedPath is required.";
            return false;
        }

        if (PollIntervalSeconds < 15)
        {
            error = "PollIntervalSeconds must be at least 15 seconds.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
