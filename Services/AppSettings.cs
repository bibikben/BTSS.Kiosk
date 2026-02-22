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
    public const string DefaultPrinterNameKey = "default_printer_name";
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
}

/// <summary>
/// Runtime flags (not persisted) mainly for Windows autostart command line.
/// </summary>
public static class RuntimeFlags
{
    public static bool IsAutoStartInvocation { get; set; }
}
