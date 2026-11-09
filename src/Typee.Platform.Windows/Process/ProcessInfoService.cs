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
        if (TryGetNonUiaSkipReason(out var hwnd, out var processName) is { } nonUia)
        {
            return nonUia;
        }

        if (IsPasswordFieldFailClosed())
        {
            return TargetSkipReason.PasswordField;
        }

        return null;
    }

    /// <summary>
    /// Privacy checks safe on the UIA STA thread (no nested Invoke).
    /// When <paramref name="focusedElement"/> is supplied, password is evaluated on that
    /// instance only (callers must use the same element for subsequent UIA work).
    /// </summary>
    public TargetSkipReason? GetSkipReasonOnUiaThread(
        CUIAutomation8 automation,
        IUIAutomationElement? focusedElement = null)
    {
        if (TryGetNonUiaSkipReason(out _, out _) is { } nonUia)
        {
            return nonUia;
        }

        var element = focusedElement ?? _elementCache.GetFocusedElement(automation);
        if (IsPasswordElementFailClosed(element))
        {
            return TargetSkipReason.PasswordField;
        }

        return null;
    }

    /// <summary>
    /// Resolves the focused element once, applies all privacy checks, and returns that
    /// same instance for callers to use (closes mid-Invoke TOCTOU).
    /// </summary>
    public TargetSkipReason? TryGetSafeFocusedElement(
        CUIAutomation8 automation,
        out IUIAutomationElement? element)
    {
        element = null;
        if (TryGetNonUiaSkipReason(out _, out _) is { } nonUia)
        {
            return nonUia;
        }

        element = _elementCache.GetFocusedElement(automation);
        if (IsPasswordElementFailClosed(element))
        {
            element = null;
            return TargetSkipReason.PasswordField;
        }

        return null;
    }

    private TargetSkipReason? TryGetNonUiaSkipReason(out nint hwnd, out string? processName)
    {
        hwnd = GetForegroundWindow();
        processName = null;
        if (hwnd == 0)
        {
            return TargetSkipReason.NoForegroundWindow;
        }

        var settings = _settingsStore.Current;

        if (settings.PauseInFullscreen && IsFullScreenPresentation())
        {
            return TargetSkipReason.FullScreenPause;
        }

        processName = TryGetProcessName(hwnd);
        if (processName is not null && IsBlocklisted(processName, settings.BlockedProcesses))
        {
            _logger.LogDebug("Skipping blocklisted process {Process}", processName);
            return TargetSkipReason.BlocklistedProcess;
        }

        // Fail closed: missing PID is unverifiable elevation.
        if (TryGetProcessId(hwnd) is not uint pid || ProcessElevation.IsElevatedOrUnknown(pid))
        {
            _logger.LogDebug("Skipping elevated or unverifiable target process {Process}", processName ?? "(unknown)");
            return TargetSkipReason.ElevatedTarget;
        }

        // Exclude Typee's own windows from caret / overlay targeting (§9.11).
        if (pid == (uint)Environment.ProcessId)
        {
            return TargetSkipReason.OwnProcess;
        }

        return null;
    }

    /// <summary>True when overlays should hide because fullscreen pause is active.</summary>
    public bool ShouldPauseOverlays()
    {
        if (!_settingsStore.Current.PauseInFullscreen)
        {
            return false;
        }

        return IsFullScreenPresentation();
    }

    private static bool IsBlocklisted(string processName, IReadOnlyList<string> blocked) =>
        blocked.Any(entry => string.Equals(entry, processName, StringComparison.OrdinalIgnoreCase));

    /// <summary>Foreground process file name (e.g. notepad.exe), or null. Never returns user text.</summary>
    public string? TryGetForegroundProcessName()
    {
        var hwnd = GetForegroundWindow();
        return hwnd == 0 ? null : TryGetProcessName(hwnd);
    }

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

    private bool IsFullScreenPresentation()
    {
        if (SHQueryUserNotificationState(out var state) != 0)
        {
            return false;
        }

        return state is QunsRunningD3dFullScreen or QunsPresentationMode;
    }

    private bool IsPasswordFieldFailClosed()
    {
        // Tuple.Completed distinguishes timeout/default from a real "not password" result.
        var probe = _uiaThread.Invoke(automation =>
        {
            var element = _elementCache.GetFocusedElement(automation);
            return (Completed: true, IsPassword: IsPasswordElementFailClosed(element));
        });

        if (!probe.Completed)
        {
            return true;
        }

        return probe.IsPassword;
    }

    /// <summary>
    /// Fail closed: only an explicit non-password property value allows work.
    /// Null element, COM/property failure, or unrecognized VARIANT → treat as password.
    /// </summary>
    internal static bool IsPasswordElementFailClosed(IUIAutomationElement? element)
    {
        if (element is null)
        {
            return true;
        }

        try
        {
            var value = element.GetCurrentPropertyValue(UIA_PropertyIds.UIA_IsPasswordPropertyId);
            return value switch
            {
                false => false,
                0 => false,
                true => true,
                1 => true,
                _ => true
            };
        }
        catch
        {
            return true;
        }
    }
}
