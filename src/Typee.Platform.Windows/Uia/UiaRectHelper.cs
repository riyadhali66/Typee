using Interop.UIAutomationClient;
using Typee.Core.Models;

namespace Typee.Platform.Windows.Uia;

internal static class UiaRectHelper
{
    public static ScreenRect? FirstBoundingRect(double[]? rects)
    {
        if (rects is null || rects.Length < 4)
        {
            return null;
        }

        return new ScreenRect(rects[0], rects[1], rects[2], rects[3]);
    }

    public static ScreenRect CaretMarkerRect(ScreenRect source, double width = 3, double height = 18)
    {
        if (source.IsEmpty)
        {
            return new ScreenRect(source.Left, source.Top, width, height);
        }

        var left = source.Right - width;
        var top = source.Top + Math.Max(0, (source.Height - height) / 2);
        return new ScreenRect(left, top, width, height);
    }

    public static ScreenRect FromTagRect(tagRECT rect) =>
        new(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
}
