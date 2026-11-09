using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Llm;
using Typee.Core.Models;

namespace Typee.Core.Suggestions.Providers;

/// <summary>
/// Level 2 completion: caret at end of text, no grammar issue, ≤ ~15 words.
/// </summary>
public sealed class LlmCompletionSuggestionProvider : ISuggestionProvider
{
    public const int MaxCompletionWords = 15;

    private readonly ILlmClient _llm;
    private readonly ILlmRateGuard _rateGuard;
    private readonly IPromptTemplateProvider _prompts;
    private readonly ICompletionCacheRepository _cache;
    private readonly TimeProvider _time;
    private readonly ILogger<LlmCompletionSuggestionProvider> _logger;

    public LlmCompletionSuggestionProvider(
        ILlmClient llm,
        ILlmRateGuard rateGuard,
        IPromptTemplateProvider prompts,
        ICompletionCacheRepository cache,
        ILogger<LlmCompletionSuggestionProvider> logger,
        TimeProvider? time = null)
    {
        _llm = llm;
        _rateGuard = rateGuard;
        _prompts = prompts;
        _cache = cache;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    public string Id => "llm-completion";
    public int Priority => SuggestionProviderPriorities.LlmCompletion;

    public async Task<Suggestion?> TryProvideAsync(
        SuggestionPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.Settings.Suggestions.Level2CompletionEnabled
            || !context.LlmAllowed
            || context.GrammarIssueFound
            || !_llm.IsOnline)
        {
            return null;
        }

        if (!IsCaretAtEnd(context.Text))
        {
            return null;
        }

        var before = context.Text.TextBeforeCaret;
        if (string.IsNullOrWhiteSpace(before))
        {
            return null;
        }

        if (!_rateGuard.TryAcquire(IApiKeyStore.DefaultProfile))
        {
            _logger.LogInformation("LLM completion skipped (rate guard)");
            return null;
        }

        var system = _prompts.GetTemplate("completion-system");
        var userTemplate = _prompts.GetTemplate("completion-user");
        var user = userTemplate.Replace("{{text}}", before, StringComparison.Ordinal);

        string raw;
        try
        {
            raw = await _llm.CompleteAsync(
                new LlmCompletionRequest
                {
                    SystemPrompt = system,
                    UserPrompt = user,
                    MaxTokens = Math.Min(context.Settings.Llm.MaxTokens, 60),
                    Temperature = context.Settings.Llm.Temperature
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM completion failed");
            return null;
        }

        var completion = NormalizeCompletion(raw);
        if (string.IsNullOrWhiteSpace(completion))
        {
            return null;
        }

        try
        {
            await _cache.UpsertAsync(
                new CompletionCacheEntry
                {
                    ContextHash = ContextHash.Compute(before),
                    Suggestion = completion,
                    Level = SuggestionLevel.Completion,
                    CreatedUtc = _time.GetUtcNow()
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write completion cache");
        }

        return new Suggestion
        {
            HeaderKey = "Suggestion_Completion",
            Body = completion,
            Prefix = string.Empty,
            Highlight = completion,
            Suffix = string.Empty,
            Kind = SuggestionKind.Completion,
            Level = SuggestionLevel.Completion,
            AnchorCaret = context.Caret?.CaretRect,
            Monitor = context.Caret?.Monitor
        };
    }

    internal static bool IsCaretAtEnd(TextContext text)
    {
        var paragraph = text.CurrentParagraph ?? string.Empty;
        if (paragraph.Length == 0)
        {
            return false;
        }

        // Caret is at end of the current paragraph (no text after caret in that paragraph).
        var caretInPara = GrammarSuggestionProvider.ResolveCaretInParagraph(text);
        return caretInPara >= paragraph.Length;
    }

    internal static string NormalizeCompletion(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var text = raw.Trim().Trim('"', '\'', '`', '“', '”');
        // Collapse to first line; strip leading ellipsis artifacts.
        var nl = text.IndexOfAny(['\r', '\n']);
        if (nl >= 0)
        {
            text = text[..nl].Trim();
        }

        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > MaxCompletionWords)
        {
            text = string.Join(' ', words.Take(MaxCompletionWords));
        }

        return text;
    }
}
