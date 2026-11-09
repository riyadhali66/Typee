namespace Typee.Core.Models;

/// <summary>Per-process preferred caret method. Stores process names only — never user text.</summary>
public sealed class CaretMethodMemory
{
    public required string ProcessName { get; init; }
    public CaretDetectionMethod Method { get; init; }
    public DateTimeOffset UpdatedUtc { get; init; }
}
