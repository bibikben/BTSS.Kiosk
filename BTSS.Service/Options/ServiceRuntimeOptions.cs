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

    public string SftpIngestPath { get; set; } = "/api/ingest";

    public string? SftpIngestScope { get; set; } = "call.ingest";

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

    public SftpImportOptions SftpImport { get; set; } = new();

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

        if (!SftpImport.Validate(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }
}

public sealed class SftpImportOptions
{
    public bool Enabled { get; set; }

    [Range(15, 3600)]
    public int PollIntervalSeconds { get; set; } = 60;

    [Range(1, 1000)]
    public int MaxFilesPerCycle { get; set; } = 100;

    [Range(1, 50)]
    public int MaxIncidentsPerFile { get; set; } = 25;

    public bool PostToApi { get; set; } = true;

    public bool ReprocessWhenRemoteFileChanges { get; set; } = true;

    public bool TrustUnknownHostKey { get; set; }

    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 22;

    public string Username { get; set; } = string.Empty;

    public string AuthenticationMode { get; set; } = "Password";

    public string? Password { get; set; }

    public string? PrivateKeyPath { get; set; }

    public string? PrivateKeyPassphrase { get; set; }

    public string RemoteDirectory { get; set; } = "/incoming";

    public string FileSearchPattern { get; set; } = "*.json";

    public bool Recursive { get; set; }

    public string? HostKeyFingerprint { get; set; }

    public bool Validate(out string error)
    {
        if (!Enabled)
        {
            error = string.Empty;
            return true;
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            error = "Service:SftpImport:Host is required when SFTP import is enabled.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            error = "Service:SftpImport:Username is required when SFTP import is enabled.";
            return false;
        }

        var authMode = (AuthenticationMode ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(authMode))
        {
            authMode = "Password";
        }

        var requiresPassword = authMode.Equals("Password", StringComparison.OrdinalIgnoreCase)
            || authMode.Equals("KeyboardInteractive", StringComparison.OrdinalIgnoreCase);
        var requiresKey = authMode.Equals("PrivateKey", StringComparison.OrdinalIgnoreCase)
            || authMode.Equals("PrivateKeyWithPassphrase", StringComparison.OrdinalIgnoreCase);

        if (!requiresPassword && !requiresKey)
        {
            error = "Service:SftpImport:AuthenticationMode must be one of Password, KeyboardInteractive, PrivateKey, or PrivateKeyWithPassphrase.";
            return false;
        }

        if (requiresPassword && string.IsNullOrWhiteSpace(Password))
        {
            error = $"Service:SftpImport:Password is required when AuthenticationMode is {authMode}.";
            return false;
        }

        if (requiresKey && string.IsNullOrWhiteSpace(PrivateKeyPath))
        {
            error = $"Service:SftpImport:PrivateKeyPath is required when AuthenticationMode is {authMode}.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(RemoteDirectory))
        {
            error = "Service:SftpImport:RemoteDirectory is required when SFTP import is enabled.";
            return false;
        }

        if (PollIntervalSeconds < 15)
        {
            error = "Service:SftpImport:PollIntervalSeconds must be at least 15 seconds.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
