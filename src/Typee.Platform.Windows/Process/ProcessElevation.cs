using System.Runtime.InteropServices;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.Process;

/// <summary>Shared fail-closed elevation probe used by privacy guards and the hook watchdog.</summary>
internal static class ProcessElevation
{
    /// <summary>True if elevated OR elevation cannot be determined.</summary>
    public static bool IsElevatedOrUnknown(uint processId)
    {
        var hProcess = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (hProcess == 0)
        {
            return true;
        }

        try
        {
            if (!OpenProcessToken(hProcess, TokenQuery, out var token))
            {
                return true;
            }

            try
            {
                var size = (uint)Marshal.SizeOf<TokenElevation>();
                var ptr = Marshal.AllocHGlobal((int)size);
                try
                {
                    if (!GetTokenInformation(token, TokenElevationInformationClass, ptr, size, out _))
                    {
                        return true;
                    }

                    var elevation = Marshal.PtrToStructure<TokenElevation>(ptr);
                    return elevation.TokenIsElevated != 0;
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            finally
            {
                _ = CloseHandle(token);
            }
        }
        finally
        {
            _ = CloseHandle(hProcess);
        }
    }

    /// <summary>True when the foreground window's process is elevated or unverifiable.</summary>
    public static bool IsForegroundElevatedOrUnknown()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == 0)
        {
            return true;
        }

        _ = GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
        {
            return true;
        }

        return IsElevatedOrUnknown(pid);
    }
}
