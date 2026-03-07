#if WINDOWS
using Microsoft.Maui.Platform;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using System.Runtime.InteropServices;
using Windows.Graphics;
using WinRT.Interop;

namespace BTSS.IAR.Kiosk.Platforms.Windows;

public static class WindowKioskHelper
{
    public static void MakeKioskOnMonitor(Window mauiWindow, MonitorInfo monitor)
    {
        var nativeWindow = (mauiWindow.Handler?.PlatformView as MauiWinUIWindow)
            ?? throw new InvalidOperationException("Native window not ready.");

        var hwnd = WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(windowId);

        if (appWindow.Presenter is OverlappedPresenter p)
        {
            p.SetBorderAndTitleBar(false, false);
            p.IsResizable = false;
            p.IsMaximizable = false;
            p.IsMinimizable = false;

            // Always on top
            p.IsAlwaysOnTop = true;
        }

        // Fullscreen on selected monitor bounds
        appWindow.MoveAndResize(new RectInt32(monitor.X, monitor.Y, monitor.Width, monitor.Height));

        // Prevent closing attempts
        appWindow.Closing += (_, e) => e.Cancel = true;

        // Hide cursor for this window
        CursorHider.HideCursorForWindow(hwnd);
    }
}

internal static class CursorHider
{
    public static void HideCursorForWindow(IntPtr hwnd)
    {
        var hCursor = CreateBlankCursor();
        if (hCursor == IntPtr.Zero) return;

        SetClassLongPtr(hwnd, GCLP_HCURSOR, hCursor);
        SetCursor(hCursor);
    }

    private static IntPtr CreateBlankCursor()
    {
        // 1x1 transparent cursor
        byte[] andMask = { 0xFF };
        byte[] xorMask = { 0x00 };

        return CreateCursor(IntPtr.Zero, 0, 0, 1, 1, andMask, xorMask);
    }

    private const int GCLP_HCURSOR = -12;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateCursor(
        IntPtr hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight,
        byte[] pvANDPlane, byte[] pvXORPlane);

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr hCursor);

    [DllImport("user32.dll", EntryPoint = "SetClassLongPtrW", SetLastError = true)]
    private static extern IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
}
#endif
