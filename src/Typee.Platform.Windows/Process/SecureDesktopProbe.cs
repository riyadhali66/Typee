using System.Runtime.InteropServices;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.Process;

/// <summary>
/// Detects the Winlogon / UAC secure desktop where LL hooks do not receive input.
/// Fail closed: inability to inspect the input desktop → treat as secure.
/// </summary>
internal static class SecureDesktopProbe
{
    public static bool IsSecureDesktopActive()
    {
        var desk = OpenInputDesktop(0, false, DesktopReadObjects);
        if (desk == 0)
        {
            return true;
        }

        try
        {
            var name = TryGetDesktopName(desk);
            if (name is null)
            {
                return true;
            }

            // Default interactive desktop is "Default". Winlogon hosts UAC / Ctrl+Alt+Del.
            return !string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _ = CloseDesktop(desk);
        }
    }

    private static string? TryGetDesktopName(nint desk)
    {
        var length = 0u;
        _ = GetUserObjectInformationW(desk, UoiName, 0, 0, out length);
        if (length == 0 || length > 512)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            if (!GetUserObjectInformationW(desk, UoiName, buffer, length, out var needed) || needed == 0)
            {
                return null;
            }

            // needed includes the terminating null (UTF-16 bytes).
            var chars = (int)needed / 2;
            if (chars <= 1)
            {
                return string.Empty;
            }

            return Marshal.PtrToStringUni(buffer, chars - 1);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
