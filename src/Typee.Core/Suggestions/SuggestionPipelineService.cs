using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Insertion;
using Typee.Core.Llm;
using Typee.Core.Models;
using Typee.Core.Settings;

namespace Typee.Core.Suggestions;

/// <summary>
/// Orchestrates suggestion runs: bail rules → router. Cancellation is owned by
/// <see cref="SuggestionRequestCoordinator"/> (TypingResumed / focus / newer request).
/// </summary>
public sealed class SuggestionPipelineService
{
    private readonly SuggestionRouter _router;
    private readonly SuggestionRequestCoordinator _coordinator;
    private readonly ISettingsStore _settings;
    private readonly ITextContextReader _textReader;
    private readonly ICaretLocator _caretLocator;
    private readonly ITargetContextGuard _guard;
    private readonly IFocusSnapshotService? _focusSnapshots;
    private readonly ILlmClient? _llm;
    private readonly ILogger<SuggestionPipelineService> _logger;

    public SuggestionPipelineService(
        SuggestionRouter router,
        SuggestionRequestCoordinator coordinator,
        ISettingsStore settings,
        ITextContextReader textReader,
        ICaretLocator caretLocator,
        ITargetContextGuard guard,
        ILogger<SuggestionPipelineService> logger,
        ILlmClient? llm = null,
        IFocusSnapshotService? focusSnapshots = null)
    {
        _router = router;
        _coordinator = coordinator;
        _settings = settings;
        _textReader = textReader;
        _caretLocator = caretLocator;
        _guard = guard;
        _logger = logger;
        _llm = llm;
        _focusSnapshots = focusSnapshots;
    }

    public SuggestionRequestCoordinator Coordinator => _coordinator;

    public void Cancel() => _coordinator.CancelCurrent();

    /// <summary>
    /// Runs a suggestion pass. Returns null when bailed, cancelled, or no provider matched.
    /// Only the latest generation should update the overlay (caller checks generation).
    /// </summary>
    public async Task<SuggestionPipelineResult> RunAsync(CancellationToken externalToken = default)
    {
        var generation = _coordinator.BeginNewRequest(out var requestToken);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(requestToken, externalToken);
        var ct = linked.Token;

        try
        {
            var settings = _settings.Current;
            if (!settings.Suggestions.Enabled)
            {
                return SuggestionPipelineResult.Bailed(generation, "disabled");
            }

            if (_guard.GetSkipReason() is { } skip)
            {
                _logger.LogInformation("Suggestion bailed (skip={Skip})", skip);
                return SuggestionPipelineResult.Bailed(generation, $"skip:{skip}");
            }

            var maxChars = settings.Suggestions.MaxContextChars;
            var text = _textReader.TryRead(maxChars);
            if (text is null)
            {
                return SuggestionPipelineResult.Bailed(generation, "no-context");
            }

            var material = text.TextBeforeCaret;
            if (string.IsNullOrWhiteSpace(material)
                || material.Trim().Length < settings.Suggestions.MinCharsBeforeSuggest)
            {
                return SuggestionPipelineResult.Bailed(generation, "too-short");
            }

            var caret = _caretLocator.TryLocate();
            var llmAllowed = settings.Privacy.LlmConsentAccepted
                             && LlmEndpointPolicy.IsAllowed(settings.Llm.BaseUrl)
                             && (_llm?.IsOnline ?? false);
            var target = _focusSnapshots?.CaptureIdentity() ?? FocusIdentity.Empty;

            var context = new SuggestionPipelineContext
            {
                Text = text,
                Settings = settings,
                Caret = caret,
                Target = target,
                LlmAllowed = llmAllowed
            };

            var suggestion = await _router.RouteAsync(context, ct).ConfigureAwait(false);
            if (!_coordinator.IsCurrent(generation))
            {
                return SuggestionPipelineResult.Stale(generation);
            }

            return suggestion is null
                ? SuggestionPipelineResult.Bailed(generation, "no-match")
                : SuggestionPipelineResult.Found(generation, suggestion);
        }
        catch (OperationCanceledException)
        {
            return SuggestionPipelineResult.Cancelled(generation);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Suggestion pipeline failed");
            return SuggestionPipelineResult.Bailed(generation, "error");
        }
    }
}

public sealed class SuggestionPipelineResult
{
    public required long Generation { get; init; }
    public Suggestion? Suggestion { get; init; }
    public required string Outcome { get; init; }

    public bool IsSuccess => Suggestion is not null && Outcome == "found";

    public static SuggestionPipelineResult Found(long generation, Suggestion suggestion) => new()
    {
        Generation = generation,
        Suggestion = suggestion,
        Outcome = "found"
    };

    public static SuggestionPipelineResult Bailed(long generation, string reason) => new()
    {
        Generation = generation,
        Outcome = reason
    };

    public static SuggestionPipelineResult Cancelled(long generation) => new()
    {
        Generation = generation,
        Outcome = "cancelled"
    };

    public static SuggestionPipelineResult Stale(long generation) => new()
    {
        Generation = generation,
        Outcome = "stale"
    };
}
