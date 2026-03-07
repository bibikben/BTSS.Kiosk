#if WINDOWS
using Microsoft.Win32;

namespace BTSS.IAR.Kiosk.Platforms.Windows;

public interface IAutoStartService
{
    bool IsEnabled();
    void SetEnabled(bool enabled);
}

/// <summary>
/// Enables/disables autostart at Windows login via HKCU Run key.
/// Works for unpackaged apps.
/// </summary>
public sealed class AutoStartService : IAutoStartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "BTSS.IAR.Kiosk";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string s && !string.IsNullOrWhiteSpace(s);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                       ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Unable to determine process path.");
        // Always start with an autostart arg so we can default to minimized.
        var cmd = $"\"{exePath}\" --autostart";
        key.SetValue(ValueName, cmd);
    }
}
#endif
