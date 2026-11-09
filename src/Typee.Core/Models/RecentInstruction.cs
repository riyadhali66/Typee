namespace Typee.Core.Models;

public sealed class RecentInstruction
{
    public long Id { get; init; }
    public required string Text { get; init; }
    public DateTimeOffset UsedUtc { get; init; }
}
