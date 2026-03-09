namespace BTSS.IAR.Kiosk.Services;

/// <summary>
/// Simple persisted settings (Preferences). Values are per-user.
/// </summary>
public static class AppSettings
{
    private const string AutoStartAtLoginKey = "settings.autostart";
    private const string StartDisplayOnStartupKey = "settings.startdisplayonstartup";
    private const string StartMinimizedKey = "settings.startminimized";
    private const string SelectedMonitorIndexKey = "settings.selectedmonitorindex";
    private const string SavedUrlKey = "settings.savedurl";
    private const string ProcessingModeKey = "settings.processing.mode"; // Email | IarApi
    private const string IarApiBaseUrlKey = "settings.iar.api.baseurl";
    private const string IarApiAgencyIdKey = "settings.iar.api.agencyid";
    private const string IarApiClientIdKey = "settings.iar.api.clientid";
    private const string IarApiClientSecretKey = "settings.iar.api.clientsecret";
    private const string PauseCheckingKey = "settings.processing.paused";
    private const string KioskDisplayNameKey = "settings.kiosk.displayname";
    private const string KioskLocationKey = "settings.kiosk.location";
    private const string KioskStationCodeKey = "settings.kiosk.stationcode";
    private const string KioskStationNameKey = "settings.kiosk.stationname";
    public const string DefaultPrinterNameKey = "default_printer_name";
    private const string ExportFolderKey = "settings.paths.exportfolder";
    private const string TemplateFolderKey = "settings.paths.templatefolder";
    public static bool AutoStartAtLogin
    {
        get => Preferences.Get(AutoStartAtLoginKey, false);
        set => Preferences.Set(AutoStartAtLoginKey, value);
    }

    public static bool StartDisplayOnStartup
    {
        get => Preferences.Get(StartDisplayOnStartupKey, false);
        set => Preferences.Set(StartDisplayOnStartupKey, value);
    }

    public static bool StartMinimized
    {
        get => Preferences.Get(StartMinimizedKey, false);
        set => Preferences.Set(StartMinimizedKey, value);
    }
    public static string? DefaultPrinterName
    {
        get => Preferences.Get(DefaultPrinterNameKey, (string?)null);
        set => Preferences.Set(DefaultPrinterNameKey, value);
    }

    public static string? ExportFolder
    {
        get => Preferences.Get(ExportFolderKey, (string?)null);
        set => Preferences.Set(ExportFolderKey, value);
    }

    public static string? TemplateFolder
    {
        get => Preferences.Get(TemplateFolderKey, (string?)null);
        set => Preferences.Set(TemplateFolderKey, value);
    }
    /// <summary>
    /// -1 means not selected.
    /// </summary>
    public static int SelectedMonitorIndex
    {
        get => Preferences.Get(SelectedMonitorIndexKey, -1);
        set => Preferences.Set(SelectedMonitorIndexKey, value);
    }

    public static string SavedUrl
    {
        get => Preferences.Get(SavedUrlKey, "https://auth.iamresponding.com/login/member");
        set => Preferences.Set(SavedUrlKey, value);
    }
    /// <summary>
    /// Processing mode:
    /// - "Email" (default): Gmail IMAP Fire Station Clear Report pipeline
    /// - "IarApi": Poll the BTSS.IAR.Api every minute for close records
    /// </summary>
    public static string ProcessingMode
    {
        get => Preferences.Get(ProcessingModeKey, "Email");
        set => Preferences.Set(ProcessingModeKey, value);
    }

    public static string IarApiBaseUrl
    {
        get => Preferences.Get(IarApiBaseUrlKey, "http://localhost:5080");
        set => Preferences.Set(IarApiBaseUrlKey, value);
    }

    public static int IarApiAgencyId
    {
        get => Preferences.Get(IarApiAgencyIdKey, 0);
        set => Preferences.Set(IarApiAgencyIdKey, value);
    }

    public static string IarApiClientId
    {
        get => Preferences.Get(IarApiClientIdKey, "");
        set => Preferences.Set(IarApiClientIdKey, value);
    }

    public static string IarApiClientSecret
    {
        get => Preferences.Get(IarApiClientSecretKey, "");
        set => Preferences.Set(IarApiClientSecretKey, value);
    }


    public static string? KioskDisplayName
    {
        get => Preferences.Get(KioskDisplayNameKey, (string?)null);
        set => Preferences.Set(KioskDisplayNameKey, value);
    }

    public static string? KioskLocation
    {
        get => Preferences.Get(KioskLocationKey, (string?)null);
        set => Preferences.Set(KioskLocationKey, value);
    }

    public static string? KioskStationCode
    {
        get => Preferences.Get(KioskStationCodeKey, (string?)null);
        set => Preferences.Set(KioskStationCodeKey, value);
    }

    public static string? KioskStationName
    {
        get => Preferences.Get(KioskStationNameKey, (string?)null);
        set => Preferences.Set(KioskStationNameKey, value);
    }
    /// <summary>
    /// When true, background Email/IAR polling is paused (no mailbox/API checks are performed).
    /// </summary>
    public static bool PauseChecking
    {
        get => Preferences.Get(PauseCheckingKey, false);
        set => Preferences.Set(PauseCheckingKey, value);
    }
}

/// <summary>
/// Runtime flags (not persisted) mainly for Windows autostart command line.
/// </summary>
public static class RuntimeFlags
{
    public static bool IsAutoStartInvocation { get; set; }
}
