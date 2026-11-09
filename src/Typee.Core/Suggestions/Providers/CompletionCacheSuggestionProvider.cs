using Typee.Core.Abstractions;
using Typee.Core.Llm;
using Typee.Core.Models;

namespace Typee.Core.Suggestions.Providers;

public sealed class CompletionCacheSuggestionProvider : ISuggestionProvider
{
    private readonly ICompletionCacheRepository _cache;

    public CompletionCacheSuggestionProvider(ICompletionCacheRepository cache)
    {
        _cache = cache;
    }

    public string Id => "completion-cache";
    public int Priority => SuggestionProviderPriorities.CompletionCache;

    public async Task<Suggestion?> TryProvideAsync(
        SuggestionPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var keyMaterial = context.Text.TextBeforeCaret;
        if (string.IsNullOrWhiteSpace(keyMaterial))
        {
            return null;
        }

        var hash = ContextHash.Compute(keyMaterial);
        var entry = await _cache.TryGetAsync(hash, cancellationToken).ConfigureAwait(false);
        if (entry is null || string.IsNullOrWhiteSpace(entry.Suggestion))
        {
            return null;
        }

        return new Suggestion
        {
            HeaderKey = "Suggestion_Completion",
            Body = entry.Suggestion,
            Prefix = string.Empty,
            Highlight = entry.Suggestion,
            Suffix = string.Empty,
            Kind = SuggestionKind.Completion,
            Level = SuggestionLevel.Local,
            AnchorCaret = context.Caret?.CaretRect,
            Monitor = context.Caret?.Monitor
        };
    }
}
