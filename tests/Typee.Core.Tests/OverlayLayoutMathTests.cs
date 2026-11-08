using Typee.Core.Models;
using Typee.Core.Text;

namespace Typee.Core.Tests;

public sealed class OverlayLayoutMathTests
{
    [Fact]
    public void ClampToWorkArea_keeps_rect_inside()
    {
        var work = new ScreenRect(0, 0, 100, 100);
        var rect = new ScreenRect(90, 90, 40, 40);
        var clamped = OverlayLayoutMath.ClampToWorkArea(rect, work);
        Assert.True(clamped.Right <= work.Right);
        Assert.True(clamped.Bottom <= work.Bottom);
    }

    [Fact]
    public void PositionBelowCaret_flips_above_when_no_room()
    {
        var work = new ScreenRect(0, 0, 200, 100);
        var caret = new ScreenRect(10, 80, 2, 16);
        var positioned = OverlayLayoutMath.PositionBelowCaret(caret, 40, work);
        Assert.True(positioned.Top < caret.Top);
    }

    [Fact]
    public void PhysicalPixelsToDip_scales_by_monitor_dpi()
    {
        Assert.Equal(100, OverlayLayoutMath.PhysicalPixelsToDip(150, 144));
    }
}
