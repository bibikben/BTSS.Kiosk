using System;
using System.Reflection;
using Microsoft.Maui.Dispatching;
using System.Drawing;
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
        var resName = "BTSS.IAR.Kiosk.Resources.AppIcon.Btss.ico";
        using var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resName);
       // using var iconStream = BTSS.IAR.Kiosk.App.Current.Resources.AppIcon.Btss.ico.AsStream();
        using var icon = new Icon(iconStream);
        _trayIcon = new TrayIconWithContextMenu
        {
            ToolTip = "BTSS Kiosk Controller",
            Icon = icon.Handle,
            ContextMenu = new PopupMenu
            {
                Items =
                {
                    new PopupMenuItem("Show Admin", (_, __) => ShowAdmin()),
                    new PopupMenuItem("Hide Admin", (_, __) => HideAdmin()),
                    new PopupMenuSeparator(),
                    new PopupMenuItem("Exit", (_, __) => ExitApp()),
                }
            }
        };

        _trayIcon.Create();
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
