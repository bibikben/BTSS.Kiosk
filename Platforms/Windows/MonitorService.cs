#if WINDOWS
using System.Runtime.InteropServices;

namespace BTSS.IAR.Kiosk.Platforms.Windows;

public record MonitorInfo(IntPtr HMonitor, string DeviceName, bool IsPrimary, int X, int Y, int Width, int Height);

public static class MonitorService
{
    public static List<MonitorInfo> GetMonitors()
    {
        var list = new List<MonitorInfo>();

        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
            (hMon, _, _, _) =>
            {
                var mi = new MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(mi);

                if (GetMonitorInfo(hMon, ref mi))
                {
                    bool primary = (mi.dwFlags & 1) != 0;
                    int x = mi.rcMonitor.left;
                    int y = mi.rcMonitor.top;
                    int w = mi.rcMonitor.right - mi.rcMonitor.left;
                    int h = mi.rcMonitor.bottom - mi.rcMonitor.top;

                    list.Add(new MonitorInfo(hMon, mi.szDevice, primary, x, y, w, h));
                }

                return true;
            }, IntPtr.Zero);

        return list.OrderByDescending(m => m.IsPrimary).ThenBy(m => m.DeviceName).ToList();
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }
}
#endif
