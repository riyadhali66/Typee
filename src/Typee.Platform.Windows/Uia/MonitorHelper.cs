using Typee.Core.Models;
using Typee.Platform.Windows.Interop;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.Uia;

internal static class MonitorHelper
{
    public static MonitorInfo FromPoint(int x, int y)
    {
        var pt = new Point { X = x, Y = y };
        var hMonitor = MonitorFromPoint(pt, MonitorDefaulttonearest);
        return FromHandle(hMonitor);
    }

    public static MonitorInfo FromRect(ScreenRect rect)
    {
        var native = new Rect
        {
            Left = (int)Math.Floor(rect.Left),
            Top = (int)Math.Floor(rect.Top),
            Right = (int)Math.Ceiling(rect.Right),
            Bottom = (int)Math.Ceiling(rect.Bottom)
        };
        var hMonitor = MonitorFromRect(in native, MonitorDefaulttonearest);
        return FromHandle(hMonitor);
    }

    private static MonitorInfo FromHandle(nint hMonitor)
    {
        if (hMonitor == 0)
        {
            return new MonitorInfo
            {
                Handle = 0,
                WorkAreaPhysical = new ScreenRect(0, 0, 1920, 1080)
            };
        }

        var info = new MonitorInfoEx { CbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MonitorInfoEx>() };
        _ = MonitorNativeMethods.GetMonitorInfoW(hMonitor, ref info);

        uint dpiX = 96;
        uint dpiY = 96;
        if (GetDpiForMonitor(hMonitor, MdtEffectiveDpi, out var dx, out var dy) == 0)
        {
            dpiX = dx;
            dpiY = dy;
        }

        var work = info.WorkArea;
        return new MonitorInfo
        {
            Handle = hMonitor,
            DeviceName = info.DeviceName,
            WorkAreaPhysical = new ScreenRect(work.Left, work.Top, work.Right - work.Left, work.Bottom - work.Top),
            DpiX = dpiX,
            DpiY = dpiY
        };
    }
}
