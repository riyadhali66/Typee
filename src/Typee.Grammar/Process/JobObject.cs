using System.Diagnostics;
using System.Runtime.InteropServices;
using Typee.Grammar.Interop;

namespace Typee.Grammar.Process;

/// <summary>Windows Job Object with KILL_ON_JOB_CLOSE so child dies with Typee.</summary>
internal sealed class JobObject : IDisposable
{
    private nint _handle;
    private bool _disposed;

    public JobObject()
    {
        _handle = NativeMethods.CreateJobObjectW(0, null);
        if (_handle == 0)
        {
            throw new InvalidOperationException(
                $"CreateJobObjectW failed (Win32 {Marshal.GetLastWin32Error()}).");
        }

        var info = new NativeMethods.JobObjectExtendedLimitInformation
        {
            BasicLimitInformation = new NativeMethods.JobObjectBasicLimitInformation
            {
                LimitFlags = NativeMethods.JobObjectLimitKillOnJobClose
            }
        };

        var size = Marshal.SizeOf<NativeMethods.JobObjectExtendedLimitInformation>();
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(info, ptr, fDeleteOld: false);
            if (!NativeMethods.SetInformationJobObject(
                    _handle,
                    NativeMethods.JobObjectInfoClassExtendedLimitInformation,
                    ptr,
                    (uint)size))
            {
                throw new InvalidOperationException(
                    $"SetInformationJobObject failed (Win32 {Marshal.GetLastWin32Error()}).");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    public void Assign(System.Diagnostics.Process process)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(process);

        if (!NativeMethods.AssignProcessToJobObject(_handle, process.Handle))
        {
            throw new InvalidOperationException(
                $"AssignProcessToJobObject failed (Win32 {Marshal.GetLastWin32Error()}).");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_handle != 0)
        {
            _ = NativeMethods.CloseHandle(_handle);
            _handle = 0;
        }
    }
}
