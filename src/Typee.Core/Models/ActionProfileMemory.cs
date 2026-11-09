namespace Typee.Core.Models;

public sealed class ActionProfileMemory
{
    public required string ActionId { get; init; }
    public required string ProfileName { get; init; }
    public DateTimeOffset UpdatedUtc { get; init; }
}
