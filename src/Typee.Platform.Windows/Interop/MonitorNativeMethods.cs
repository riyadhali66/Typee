using System.Runtime.InteropServices;

namespace Typee.Platform.Windows.Interop;

internal static class MonitorNativeMethods
{
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfoW(nint hMonitor, ref NativeMethods.MonitorInfoEx lpmi);
}
