using System.Runtime.InteropServices;

namespace Typee.App.Interop;

/// <summary>Win32 imports for the WPF host process only.</summary>
internal static partial class NativeMethods
{
    public const uint LoadLibrarySearchDefaultDirs = 0x1000;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetDefaultDllDirectories(uint directoryFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
}
