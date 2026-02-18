#if WINDOWS
using System.Windows.Forms;
using Microsoft.Maui.Platform;
using Microsoft.UI.Windowing;
using WinRT.Interop;

namespace BTSS.IAR.Kiosk.Platforms.Windows;

public interface ITrayIconService : IDisposable
{
    void Initialize(Window mainMauiWindow);
    void ShowAdmin();
    void HideAdmin();
    void Quit();
    bool IsInitialized { get; }
}

/// <summary>
/// System tray icon + context menu. On window close, hides to tray.
/// Quit is only supported exit.
/// </summary>
public sealed class TrayIconService : ITrayIconService
{
    private NotifyIcon? _notify;
    private AppWindow? _appWindow;
    private Window? _mauiWindow;
    private bool _quitting;

    public bool IsInitialized => _notify != null;
    public bool IsQuitting => _quitting;

    public void Initialize(Window mainMauiWindow)
    {
        if (_notify != null) return;

        _mauiWindow = mainMauiWindow;

        if (mainMauiWindow.Handler?.PlatformView is not MauiWinUIWindow winuiWindow)
            return;

        var hwnd = WindowNative.GetWindowHandle(winuiWindow);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        _appWindow.Closing += (_, e) =>
        {
            if (_quitting) return;
            e.Cancel = true;
            HideAdmin();
        };

        _notify = new NotifyIcon
        {
            Text = "BTSS IAR Kiosk",
            Visible = true,
            Icon = System.Drawing.SystemIcons.Application,
            ContextMenuStrip = BuildMenu()
        };

        _notify.DoubleClick += (_, _) => ShowAdmin();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var showAdmin = new ToolStripMenuItem("Show Admin");
        showAdmin.Click += (_, _) => ShowAdmin();

        var startDisplay = new ToolStripMenuItem("Start display");
        startDisplay.Click += async (_, _) =>
        {
            if (Application.Current is BTSS.IAR.Kiosk.App app)
                await app.TryStartDisplayFromSavedAsync(showAdminIfMissingConfig: true);
        };

        var stopDisplay = new ToolStripMenuItem("Stop display");
        stopDisplay.Click += (_, _) =>
        {
            if (Application.Current is BTSS.IAR.Kiosk.App app)
                app.StopDisplayWindow();
        };

        var quit = new ToolStripMenuItem("Quit");
        quit.Click += (_, _) => Quit();

        menu.Items.Add(showAdmin);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(startDisplay);
        menu.Items.Add(stopDisplay);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(quit);

        return menu;
    }

    public void ShowAdmin()
    {
        if (_appWindow == null) return;
        _appWindow.Show();
        // Activate window
        if (_mauiWindow?.Handler?.PlatformView is MauiWinUIWindow winuiWindow)
            winuiWindow.Activate();
    }

    public void HideAdmin()
    {
        if (_appWindow == null) return;
        _appWindow.Hide();
    }

    public void Quit()
    {
        _quitting = true;
        try
        {
            _notify?.Dispose();
        }
        catch { }

        // Ensure process exits even if other windows veto closing.
        Environment.Exit(0);
    }

    public void Dispose()
    {
        try { _notify?.Dispose(); } catch { }
        _notify = null;
    }
}
#endif
