using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Models;

namespace Typee.Core.Suggestions;

/// <summary>
/// Runs registered <see cref="ISuggestionProvider"/>s in priority order; first non-null wins.
/// Level 1 grammar providers use lower priority numbers than Level 2 completion.
/// </summary>
public sealed class SuggestionRouter
{
    private readonly IReadOnlyList<ISuggestionProvider> _providers;
    private readonly ILogger<SuggestionRouter> _logger;

    public SuggestionRouter(
        IEnumerable<ISuggestionProvider> providers,
        ILogger<SuggestionRouter> logger)
    {
        _providers = providers.OrderBy(p => p.Priority).ThenBy(p => p.Id, StringComparer.Ordinal).ToArray();
        _logger = logger;
    }

    public IReadOnlyList<ISuggestionProvider> Providers => _providers;

    public async Task<Suggestion?> RouteAsync(
        SuggestionPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Suggestion? suggestion;
            try
            {
                suggestion = await provider.TryProvideAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Suggestion provider {ProviderId} failed", provider.Id);
                continue;
            }

            if (suggestion is not null)
            {
                _logger.LogInformation(
                    "Suggestion routed from {ProviderId} (kind={Kind}, level={Level})",
                    provider.Id,
                    suggestion.Kind,
                    suggestion.Level);
                return suggestion;
            }
        }

        return null;
    }
}
