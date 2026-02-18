#if WINDOWS
using System;
using Microsoft.Maui.Dispatching;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace BTSS.IAR.Kiosk.Platforms.Windows;

public interface ITrayService
{
    void Initialize(nint hwnd);
    void ShowAdmin();
    void HideAdmin();
    void ExitApp();
}

public sealed class TrayService : ITrayService, IDisposable
{
    private TrayIcon? _trayIcon;

    public void Initialize(nint hwnd)
    {
        if (_trayIcon != null) return;

        _trayIcon = new TrayIcon
        {
            ToolTipText = "BTSS Kiosk Controller",
            // Icon = ... set from embedded resource / file
        };

        var menu = new PopupMenu();
        menu.Items.Add(new PopupMenuItem("Show Admin", (_, __) => ShowAdmin()));
        menu.Items.Add(new PopupMenuItem("Hide Admin", (_, __) => HideAdmin()));
        menu.Items.Add(new PopupMenuItemSeparator());
        menu.Items.Add(new PopupMenuItem("Exit", (_, __) => ExitApp()));
        _trayIcon.ContextMenu = menu;

        _trayIcon.LeftClickCommand = new RelayCommand(_ => ShowAdmin());

        _trayIcon.ForceCreate(); // ensures it shows up
    }

    public void ShowAdmin()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // TODO: restore/activate your Admin window
            // e.g. bring MainPage/AdminPage window to front
        });
    }

    public void HideAdmin()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // TODO: hide/minimize your Admin window
        });
    }

    public void ExitApp()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // graceful shutdown
            Environment.Exit(0);
        });
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
    }
}
#endif