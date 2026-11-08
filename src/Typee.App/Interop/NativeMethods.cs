using System.Runtime.InteropServices;

namespace Typee.App.Interop;

/// <summary>Win32 imports for the WPF host process only.</summary>
internal static partial class NativeMethods
{
    public const uint LoadLibrarySearchDefaultDirs = 0x1000;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetDefaultDllDirectories(uint directoryFlags);
}
