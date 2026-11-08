namespace Typee.Core.Models;

public sealed class CaretLocation
{
    public required ScreenRect CaretRect { get; init; }
    public required MonitorInfo Monitor { get; init; }
    public CaretDetectionMethod Method { get; init; }
}
