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
            Icon = icon.Handle
        };

        _trayIcon.ContextMenu = new PopupMenu
        {
            Items =
            {
                new PopupMenuItem("Create Second", (_, _) => CreateSecond()),
                new PopupMenuSeparator(),
                new PopupMenuItem("Show Message", (_, _) => ShowMessage(trayIcon, "message")),
                new PopupMenuItem("Show Info", (_, _) => ShowInfo(trayIcon, "info")),
                new PopupMenuItem("Show Warning", (_, _) => ShowWarning(trayIcon, "warning")),
                new PopupMenuItem("Show Error", (_, _) => ShowError(trayIcon, "error")),
                new PopupMenuItem("Show Custom", (_, _) => ShowCustom(trayIcon, "custom", icon)),
                new PopupMenuSeparator(),
                new PopupSubMenu("SubMenu")
                {
                    Items =
                    {
                        new PopupMenuItem("Item 1", (_, _) => ShowMessage(trayIcon, "Item 1")),
                        new PopupSubMenu("SubMenu 2")
                        {
                            Items =
                            {
                                new PopupMenuItem("Item 2", (_, _) => ShowMessage(trayIcon, "Item 2")),
                            }
                        }
                    }
                },
                new PopupMenuSeparator(),
                new PopupMenuItem("Remove", (_, _) => Remove(trayIcon)),
                new PopupMenuItem("Hide", (_, _) => Hide(trayIcon)),
                new PopupMenuSeparator(),
                new PopupMenuItem("Exit", (_, _) =>
                {
                    trayIcon.Dispose();
                    Environment.Exit(0);
                }),
            },
        };
        trayIcon.Create();
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
