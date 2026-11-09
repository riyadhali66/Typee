namespace Typee.Core.Models;

public sealed class Phrase
{
    public long Id { get; init; }
    public required string Text { get; init; }
    public required string TriggerPrefix { get; init; }
    public int UseCount { get; init; }
    public DateTimeOffset? LastUsedUtc { get; init; }
    public PhraseSource Source { get; init; }
}
