using System.Runtime.InteropServices;
using Interop.UIAutomationClient;
using Microsoft.Extensions.Logging;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.Uia;

/// <summary>Invalidates the UIA element cache when foreground or focus changes.</summary>
public sealed class FocusMonitor : IDisposable
{
    private readonly UiaElementCache _cache;
    private readonly UiaThread _uiaThread;
    private readonly ILogger<FocusMonitor> _logger;
    private readonly WinEventProc _proc;
    private nint _foregroundHook;
    private nint _focusHook;
    private bool _disposed;

    public FocusMonitor(UiaElementCache cache, UiaThread uiaThread, ILogger<FocusMonitor> logger)
    {
        _cache = cache;
        _uiaThread = uiaThread;
        _logger = logger;
        _proc = OnWinEvent;

        _uiaThread.Invoke(automation =>
        {
            _ = automation;
            var flags = WineventOutofcontext | WineventSkipownprocess;
            _foregroundHook = SetWinEventHook(
                EventSystemForeground,
                EventSystemForeground,
                0,
                _proc,
                0,
                0,
                flags);
            _focusHook = SetWinEventHook(
                EventObjectFocus,
                EventObjectFocus,
                0,
                _proc,
                0,
                0,
                flags);

            if (_foregroundHook == 0 || _focusHook == 0)
            {
                _logger.LogWarning(
                    "SetWinEventHook failed (foreground={FgErr}, focus={FocusErr})",
                    Marshal.GetLastWin32Error(),
                    Marshal.GetLastWin32Error());
            }
            else
            {
                _logger.LogInformation("Focus monitor hooks installed");
            }
        });
    }

    private void OnWinEvent(
        nint hWinEventHook,
        uint eventType,
        nint hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        _cache.Invalidate();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _uiaThread.Invoke(UnhookHooks);
    }

    private void UnhookHooks(CUIAutomation8 automation)
    {
        _ = automation;
        if (_foregroundHook != 0)
        {
            UnhookWinEvent(_foregroundHook);
            _foregroundHook = 0;
        }

        if (_focusHook != 0)
        {
            UnhookWinEvent(_focusHook);
            _focusHook = 0;
        }
    }
}
