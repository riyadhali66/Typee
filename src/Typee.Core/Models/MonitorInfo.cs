namespace Typee.Core.Models;

public sealed class MonitorInfo
{
    public nint Handle { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public ScreenRect WorkAreaPhysical { get; init; }
    public uint DpiX { get; init; } = 96;
    public uint DpiY { get; init; } = 96;
}
