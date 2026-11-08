using Typee.Core.Models;

namespace Typee.Core.Text;

public static class OverlayLayoutMath
{
    public static ScreenRect ClampToWorkArea(ScreenRect rect, ScreenRect workArea)
    {
        var width = Math.Min(rect.Width, workArea.Width);
        var height = Math.Min(rect.Height, workArea.Height);
        var left = Math.Clamp(rect.Left, workArea.Left, workArea.Right - width);
        var top = Math.Clamp(rect.Top, workArea.Top, workArea.Bottom - height);
        return new ScreenRect(left, top, width, height);
    }

    public static ScreenRect PositionBelowCaret(ScreenRect caret, double overlayHeight, ScreenRect workArea, double gap = 4)
    {
        var width = Math.Min(360, workArea.Width);
        var left = caret.Left;
        var top = caret.Bottom + gap;
        var rect = new ScreenRect(left, top, width, overlayHeight);
        if (rect.Bottom <= workArea.Bottom)
        {
            return ClampToWorkArea(rect, workArea);
        }

        var aboveTop = caret.Top - gap - overlayHeight;
        rect = new ScreenRect(left, aboveTop, width, overlayHeight);
        return ClampToWorkArea(rect, workArea);
    }

    public static double PhysicalPixelsToDip(double physicalPixels, uint monitorDpi) =>
        monitorDpi == 0 ? physicalPixels : physicalPixels * 96.0 / monitorDpi;
}
