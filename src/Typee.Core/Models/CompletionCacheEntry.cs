namespace Typee.Core.Models;

public sealed class CompletionCacheEntry
{
    public required string ContextHash { get; init; }
    public required string Suggestion { get; init; }
    public SuggestionLevel Level { get; init; }
    public DateTimeOffset CreatedUtc { get; init; }
}
