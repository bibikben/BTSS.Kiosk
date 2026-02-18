#if WINDOWS
using System.Runtime.InteropServices;

namespace BTSS.IAR.Kiosk.Platforms.Windows;

public sealed class GlobalHotKey : IDisposable
{
    private readonly IntPtr _hwnd;
    private readonly int _id;
    private readonly WndProcDelegate _newWndProc;
    private readonly IntPtr _oldWndProc;
    private bool _disposed;

    public event Action? Pressed;

    public GlobalHotKey(IntPtr hwnd, int id, uint modifiers, uint vk)
    {
        _hwnd = hwnd;
        _id = id;

        if (!RegisterHotKey(_hwnd, _id, modifiers, vk))
            throw new InvalidOperationException("RegisterHotKey failed.");

        _newWndProc = WndProc;
        _oldWndProc = SetWindowLongPtr(_hwnd, GWLP_WNDPROC, Marshal.GetFunctionPointerForDelegate(_newWndProc));
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        const uint WM_HOTKEY = 0x0312;

        if (msg == WM_HOTKEY && wParam.ToInt32() == _id)
            Pressed?.Invoke();

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnregisterHotKey(_hwnd, _id);
        SetWindowLongPtr(_hwnd, GWLP_WNDPROC, _oldWndProc);
    }

    private const int GWLP_WNDPROC = -4;

    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
}
#endif
