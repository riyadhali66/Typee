namespace Typee.Core.Models;

/// <summary>Axis-aligned rectangle in physical screen pixels (Per-Monitor V2).</summary>
public readonly record struct ScreenRect(double Left, double Top, double Width, double Height)
{
    public double Right => Left + Width;
    public double Bottom => Top + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public (int X, int Y) BottomLeftPixel => ((int)Left, (int)Bottom);

    public ScreenRect WithSize(double width, double height) => new(Left, Top, width, height);

    public ScreenRect Inset(double dx, double dy) =>
        new(Left + dx, Top + dy, Math.Max(0, Width - 2 * dx), Math.Max(0, Height - 2 * dy));
}
