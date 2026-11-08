using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Core.Settings;
using Typee.Platform.Windows.Uia;
using Interop.UIAutomationClient;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.Process;

public sealed class ProcessInfoService : ITargetContextGuard
{
    private readonly ISettingsStore _settingsStore;
    private readonly UiaThread _uiaThread;
    private readonly UiaElementCache _elementCache;
    private readonly ILogger<ProcessInfoService> _logger;

    public ProcessInfoService(
        ISettingsStore settingsStore,
        UiaThread uiaThread,
        UiaElementCache elementCache,
        ILogger<ProcessInfoService> logger)
    {
        _settingsStore = settingsStore;
        _uiaThread = uiaThread;
        _elementCache = elementCache;
        _logger = logger;
    }

    public TargetSkipReason? GetSkipReason()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == 0)
        {
            return TargetSkipReason.NoForegroundWindow;
        }

        var settings = _settingsStore.Current;

        if (settings.PauseInFullscreen && IsFullScreenPresentation())
        {
            return TargetSkipReason.FullScreenPause;
        }

        var processName = TryGetProcessName(hwnd);
        if (processName is not null && IsBlocklisted(processName, settings.BlockedProcesses))
        {
            _logger.LogDebug("Skipping blocklisted process {Process}", processName);
            return TargetSkipReason.BlocklistedProcess;
        }

        if (processName is not null && TryGetProcessId(hwnd) is uint pid && IsProcessElevated(pid))
        {
            _logger.LogDebug("Skipping elevated target process {Process}", processName);
            return TargetSkipReason.ElevatedTarget;
        }

        if (IsPasswordField())
        {
            return TargetSkipReason.PasswordField;
        }

        return null;
    }

    private static bool IsBlocklisted(string processName, IReadOnlyList<string> blocked) =>
        blocked.Any(entry => string.Equals(entry, processName, StringComparison.OrdinalIgnoreCase));

    private static string? TryGetProcessName(nint hwnd)
    {
        _ = GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
        {
            return null;
        }

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)pid);
            return process.ProcessName + ".exe";
        }
        catch
        {
            return null;
        }
    }

    private static uint? TryGetProcessId(nint hwnd)
    {
        _ = GetWindowThreadProcessId(hwnd, out var pid);
        return pid == 0 ? null : pid;
    }

    private static bool IsProcessElevated(uint processId)
    {
        var hProcess = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (hProcess == 0)
        {
            return false;
        }

        try
        {
            if (!OpenProcessToken(hProcess, TokenQuery, out var token))
            {
                return false;
            }

            try
            {
                var elevation = new TokenElevation();
                var size = (uint)Marshal.SizeOf<TokenElevation>();
                var ptr = Marshal.AllocHGlobal((int)size);
                try
                {
                    if (!GetTokenInformation(token, TokenElevationInformationClass, ptr, size, out _))
                    {
                        return false;
                    }

                    elevation = Marshal.PtrToStructure<TokenElevation>(ptr);
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

    private bool IsFullScreenPresentation()
    {
        if (SHQueryUserNotificationState(out var state) != 0)
        {
            return false;
        }

        return state is QunsRunningD3dFullScreen or QunsPresentationMode;
    }

    private bool IsPasswordField()
    {
        return _uiaThread.Invoke(automation =>
        {
            var element = _elementCache.GetFocusedElement(automation);
            if (element is null)
            {
                return false;
            }

            try
            {
                var value = element.GetCurrentPropertyValue(UIA_PropertyIds.UIA_IsPasswordPropertyId);
                return value is true or 1;
            }
            catch
            {
                return false;
            }
        });
    }
}
