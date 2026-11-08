using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Platform.Windows.Process;
using Typee.Platform.Windows.Uia;
using Interop.UIAutomationClient;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.Caret;

public sealed class CaretLocator : ICaretLocator
{
    private readonly UiaThread _uiaThread;
    private readonly ProcessInfoService _guard;
    private readonly ILogger<CaretLocator> _logger;

    public CaretLocator(
        UiaThread uiaThread,
        ProcessInfoService guard,
        ILogger<CaretLocator> logger)
    {
        _uiaThread = uiaThread;
        _guard = guard;
        _logger = logger;
    }

    public CaretLocation? TryLocate()
    {
        if (_guard.GetSkipReason() is not null)
        {
            return null;
        }

        return _uiaThread.Invoke(automation =>
        {
            // Resolve + privacy-check one focused element; use that instance only.
            if (_guard.TryGetSafeFocusedElement(automation, out var element) is not null)
            {
                return null;
            }

            if (TryGuiThreadInfo(out var guiRect, out var method))
            {
                return BuildLocation(guiRect, method);
            }

            if (element is null)
            {
                return null;
            }

            if (TryTextPattern2Caret(element, out var tp2Rect))
            {
                return BuildLocation(tp2Rect, CaretDetectionMethod.TextPattern2CaretRange);
            }

            if (TryTextPatternSelection(element, out var selectionRect))
            {
                return BuildLocation(selectionRect, CaretDetectionMethod.TextPatternSelection);
            }

            if (TryFocusedBounds(element, out var boundsRect))
            {
                return BuildLocation(boundsRect, CaretDetectionMethod.FocusedElementBounds);
            }

            return null;
        });
    }

    private static CaretLocation BuildLocation(ScreenRect caretRect, CaretDetectionMethod method)
    {
        var marker = UiaRectHelper.CaretMarkerRect(caretRect);
        var monitor = MonitorHelper.FromRect(marker);
        return new CaretLocation
        {
            CaretRect = marker,
            Monitor = monitor,
            Method = method
        };
    }

    private bool TryGuiThreadInfo(out ScreenRect rect, out CaretDetectionMethod method)
    {
        rect = default;
        method = CaretDetectionMethod.GuiThreadInfo;

        var hwnd = GetForegroundWindow();
        if (hwnd == 0)
        {
            return false;
        }

        var threadId = GetWindowThreadProcessId(hwnd, out _);
        var info = new GuiThreadInfo { CbSize = System.Runtime.InteropServices.Marshal.SizeOf<GuiThreadInfo>() };
        if (!GetGUIThreadInfo(threadId, ref info))
        {
            _logger.LogDebug("GetGUIThreadInfo failed with {Error}", System.Runtime.InteropServices.Marshal.GetLastWin32Error());
            return false;
        }

        var caretWnd = info.HwndCaret != 0 ? info.HwndCaret : info.HwndFocus;
        if (caretWnd == 0)
        {
            return false;
        }

        var rc = info.RcCaret;
        if (rc.Right - rc.Left <= 0 || rc.Bottom - rc.Top <= 0)
        {
            var pt = new Point { X = rc.Left, Y = rc.Bottom > rc.Top ? rc.Bottom : rc.Top };
            _ = ClientToScreen(caretWnd, ref pt);
            rect = new ScreenRect(pt.X, pt.Y - 9, 2, 18);
            return true;
        }

        var topLeft = new Point { X = rc.Left, Y = rc.Top };
        var bottomRight = new Point { X = rc.Right, Y = rc.Bottom };
        if (!ClientToScreen(caretWnd, ref topLeft) || !ClientToScreen(caretWnd, ref bottomRight))
        {
            return false;
        }

        rect = new ScreenRect(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);
        return !rect.IsEmpty;
    }

    private static bool TryTextPattern2Caret(IUIAutomationElement element, out ScreenRect rect)
    {
        rect = default;
        var pattern = UiaPatternHelper.TryGetTextPattern2(element);
        if (pattern is null)
        {
            return false;
        }

        try
        {
            var caretRange = pattern.GetCaretRange(out _);
            var bounds = UiaRectHelper.FirstBoundingRect(caretRange?.GetBoundingRectangles());
            if (bounds is { } found && !found.IsEmpty)
            {
                rect = found;
                return true;
            }

            if (caretRange is null)
            {
                return false;
            }

            var expanded = caretRange.Clone();
            expanded.MoveEndpointByUnit(TextPatternRangeEndpoint.TextPatternRangeEndpoint_Start, TextUnit.TextUnit_Character, -1);
            bounds = UiaRectHelper.FirstBoundingRect(expanded.GetBoundingRectangles());
            if (bounds is not { } back)
            {
                return false;
            }

            rect = new ScreenRect(back.Right - 2, back.Top, 2, Math.Max(12, back.Height));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryTextPatternSelection(IUIAutomationElement element, out ScreenRect rect)
    {
        rect = default;
        var pattern = UiaPatternHelper.TryGetTextPattern(element);
        if (pattern is null)
        {
            return false;
        }

        try
        {
            var selection = pattern.GetSelection();
            if (selection.Length == 0)
            {
                return false;
            }

            var range = selection.GetElement(0);
            var bounds = UiaRectHelper.FirstBoundingRect(range?.GetBoundingRectangles());
            if (bounds is not { } found || found.IsEmpty)
            {
                return false;
            }

            rect = new ScreenRect(found.Right - 2, found.Top, 2, Math.Max(12, found.Height));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryFocusedBounds(IUIAutomationElement element, out ScreenRect rect)
    {
        rect = default;
        try
        {
            var bounds = element.CurrentBoundingRectangle;
            rect = new ScreenRect(bounds.left, bounds.bottom - 2, 2, 18);
            return !rect.IsEmpty;
        }
        catch
        {
            return false;
        }
    }
}
