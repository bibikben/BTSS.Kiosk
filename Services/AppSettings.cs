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
}

/// <summary>
/// Runtime flags (not persisted) mainly for Windows autostart command line.
/// </summary>
public static class RuntimeFlags
{
    public static bool IsAutoStartInvocation { get; set; }
}
