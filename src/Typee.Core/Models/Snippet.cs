namespace Typee.Core.Models;

public sealed class Snippet
{
    public long Id { get; init; }
    public required string Trigger { get; init; }
    public required string Body { get; init; }
    public int UseCount { get; init; }
    public DateTimeOffset UpdatedUtc { get; init; }
}
