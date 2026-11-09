using Typee.Core.Models;
using Typee.Core.Suggestions;

namespace Typee.Core.Abstractions;

/// <summary>
/// Plugin hook for suggestion levels. Built-ins use priorities 10–200;
/// contributors should document where their priority sits relative to L1/L2.
/// </summary>
public interface ISuggestionProvider
{
    string Id { get; }

    /// <summary>Lower values run first. First non-null suggestion wins.</summary>
    int Priority { get; }

    Task<Suggestion?> TryProvideAsync(
        SuggestionPipelineContext context,
        CancellationToken cancellationToken = default);
}
