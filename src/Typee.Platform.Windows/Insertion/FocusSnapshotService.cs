using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Insertion;
using Typee.Core.Models;
using Typee.Platform.Windows.Process;
using Typee.Platform.Windows.Uia;
using Interop.UIAutomationClient;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.Insertion;

/// <summary>
/// Captures/restores foreground window + focused UIA element + selection/caret.
/// Implements <see cref="IFocusSnapshotService"/> for Phase 8 / 8C / 8D.
/// </summary>
public sealed class FocusSnapshotService : IFocusSnapshotService
{
    private readonly UiaThread _uiaThread;
    private readonly ProcessInfoService _guard;
    private readonly ITextContextReader _textReader;
    private readonly ILogger<FocusSnapshotService> _logger;

    public FocusSnapshotService(
        UiaThread uiaThread,
        ProcessInfoService guard,
        ITextContextReader textReader,
        ILogger<FocusSnapshotService> logger)
    {
        _uiaThread = uiaThread;
        _guard = guard;
        _textReader = textReader;
        _logger = logger;
    }

    public FocusIdentity? CaptureIdentity()
    {
        if (_guard.GetSkipReason() is not null)
        {
            return null;
        }

        return _uiaThread.Invoke(automation =>
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == 0)
            {
                return (FocusIdentity?)null;
            }

            if (_guard.TryGetSafeFocusedElement(automation, out var element) is not null || element is null)
            {
                return new FocusIdentity(hwnd, string.Empty);
            }

            var runtimeKey = FocusIdentity.FormatRuntimeId(TryGetRuntimeId(element));
            return new FocusIdentity(hwnd, runtimeKey);
        });
    }

    public FocusSnapshot? Capture()
    {
        if (_guard.GetSkipReason() is not null)
        {
            return null;
        }

        var identity = CaptureIdentity();
        if (identity is null || identity.Value.IsEmpty)
        {
            return null;
        }

        TextContext? text = null;
        try
        {
            text = _textReader.TryRead(maxContextChars: 64);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Focus snapshot text read failed");
        }

        return new FocusSnapshot
        {
            Identity = identity.Value,
            CaretOffset = text?.CaretOffset ?? -1,
            Selection = text?.Selection,
            CapturedAt = DateTimeOffset.UtcNow
        };
    }

    public bool TryRestore(FocusSnapshot snapshot)
    {
        if (snapshot.Identity.IsEmpty || !IsWindow(snapshot.Identity.WindowHandle))
        {
            _logger.LogDebug("Focus restore aborted (invalid window)");
            return false;
        }

        if (!TryRestoreForeground(snapshot.Identity.WindowHandle))
        {
            _logger.LogDebug("Focus restore SetForegroundWindow failed");
            return false;
        }

        // Give the target a brief moment before UIA selection restore.
        Thread.Sleep(20);

        if (snapshot.Selection is { } selection && selection.Length > 0)
        {
            if (_textReader.TrySelect(selection))
            {
                _logger.LogDebug("Focus restore selected span ({Start},{Length})", selection.Start, selection.Length);
                return true;
            }
        }

        if (snapshot.CaretOffset >= 0 && _textReader.TrySetCaretOffset(snapshot.CaretOffset))
        {
            _logger.LogDebug("Focus restore caret offset={Offset}", snapshot.CaretOffset);
            return true;
        }

        _logger.LogDebug("Focus restore window only (selection/caret unavailable)");
        return true;
    }

    private bool TryRestoreForeground(nint hwnd)
    {
        var foreground = GetForegroundWindow();
        if (foreground == hwnd)
        {
            return true;
        }

        var currentThread = GetCurrentThreadId();
        uint targetThread = 0;
        if (foreground != 0)
        {
            targetThread = GetWindowThreadProcessId(foreground, out _);
        }

        var attached = false;
        try
        {
            if (targetThread != 0 && targetThread != currentThread)
            {
                attached = AttachThreadInput(currentThread, targetThread, true);
            }

            if (!SetForegroundWindow(hwnd))
            {
                _logger.LogDebug("SetForegroundWindow failed win32={Error}", Marshal.GetLastWin32Error());
                return false;
            }

            return true;
        }
        finally
        {
            if (attached)
            {
                _ = AttachThreadInput(currentThread, targetThread, false);
            }
        }
    }

    private static int[]? TryGetRuntimeId(IUIAutomationElement element)
    {
        try
        {
            var runtime = element.GetRuntimeId();
            if (runtime is null || runtime.Length == 0)
            {
                return null;
            }

            if (runtime is int[] typed)
            {
                return typed.Length == 0 ? null : typed;
            }

            var ids = new int[runtime.Length];
            for (var i = 0; i < runtime.Length; i++)
            {
                ids[i] = (int)runtime.GetValue(i)!;
            }

            return ids;
        }
        catch
        {
            return null;
        }
    }
}
