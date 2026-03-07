namespace BTSS.IAR.Models;

public sealed class AdminPreferences
{
    public string ApiBaseUrl { get; set; } = "https://localhost:5001";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = "clients.read clients.write global-settings.read global-settings.write device-settings.read device-settings.write display.read display.write kiosk.commands service.poll";
    public string ServiceDatabasePath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BTSS", "Service", "btss-service.db");
    public string ServiceConfigPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BTSS", "Service", "appsettings.json");
    public string KioskConfigPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BTSS.IAR.Kiosk", "settings.json");
    public string PreferredDeviceId { get; set; } = string.Empty;
    public int? PreferredClientNumericId { get; set; }
    public bool AutoRefreshDashboard { get; set; } = true;
    public int DashboardRefreshSeconds { get; set; } = 30;
}

public sealed class ServiceRuntimeDocument
{
    public string ApiBaseUrl { get; set; } = "https://localhost:5001/";
    public string IncidentFeedPath { get; set; } = "/api/service/incidents";
    public string OAuthTokenPath { get; set; } = "/connect/token";
    public string ClientId { get; set; } = "service-client";
    public string ClientSecret { get; set; } = string.Empty;
    public string Scope { get; set; } = "service.poll";
    public int PollIntervalSeconds { get; set; } = 60;
    public int HttpTimeoutSeconds { get; set; } = 30;
    public int MaxConsecutiveFailuresBeforeBackoff { get; set; } = 3;
    public int MaxBackoffMinutes { get; set; } = 10;
    public int MaxPrintAttempts { get; set; } = 5;
    public string LocalDataDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BTSS", "Service");
    public string DatabaseFileName { get; set; } = "btss-service.db";
    public string PrintOutputDirectory { get; set; } = "print-jobs";
    public string HealthLogDirectory { get; set; } = "health";
    public string? PrinterName { get; set; }
    public bool EnableShellPrinting { get; set; }
}

public sealed class DashboardSnapshot
{
    public List<IncidentListItem> OpenIncidents { get; set; } = new();
    public List<IncidentListItem> ClosedIncidents { get; set; } = new();
    public List<PrintJobItem> PrintJobs { get; set; } = new();
    public List<PollRunItem> PollRuns { get; set; } = new();
    public List<IncidentTransitionItem> RecentTransitions { get; set; } = new();
}

public sealed class IncidentListItem
{
    public string IncidentId { get; set; } = string.Empty;
    public string Agency { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string CallType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
    public DateTimeOffset? SourceUpdatedAtUtc { get; set; }
    public DateTimeOffset FirstSeenUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public string CanonicalJson { get; set; } = string.Empty;
}

public sealed class PrintJobItem
{
    public string IncidentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string TemplateKind { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? LastAttemptAtUtc { get; set; }
    public DateTimeOffset? PrintedAtUtc { get; set; }
    public string? Error { get; set; }
}

public sealed class PollRunItem
{
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public bool Success { get; set; }
    public int IncidentCount { get; set; }
    public string? Error { get; set; }
    public int HttpStatusCode { get; set; }
    public int PayloadBytes { get; set; }
    public string Endpoint { get; set; } = string.Empty;
}
