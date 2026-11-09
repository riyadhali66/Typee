using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Llm;
using Typee.Core.Models;

namespace Typee.Core.Suggestions.Providers;

/// <summary>
/// Level 1 deep check: only when llmDeepCheck is on and Harper found nothing.
/// </summary>
public sealed class LlmStyleCheckSuggestionProvider : ISuggestionProvider
{
    public const string DefaultProfile = IApiKeyStore.DefaultProfile;

    private readonly ILlmClient _llm;
    private readonly ILlmRateGuard _rateGuard;
    private readonly IPromptTemplateProvider _prompts;
    private readonly ILogger<LlmStyleCheckSuggestionProvider> _logger;

    public LlmStyleCheckSuggestionProvider(
        ILlmClient llm,
        ILlmRateGuard rateGuard,
        IPromptTemplateProvider prompts,
        ILogger<LlmStyleCheckSuggestionProvider> logger)
    {
        _llm = llm;
        _rateGuard = rateGuard;
        _prompts = prompts;
        _logger = logger;
    }

    public string Id => "llm-style-check";
    public int Priority => SuggestionProviderPriorities.LlmStyleCheck;

    public async Task<Suggestion?> TryProvideAsync(
        SuggestionPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.Settings.Suggestions.Level1GrammarEnabled
            || !context.Settings.Grammar.LlmDeepCheck
            || !context.LlmAllowed
            || context.GrammarIssueFound
            || !_llm.IsOnline)
        {
            return null;
        }

        var paragraph = context.Text.CurrentParagraph;
        if (string.IsNullOrWhiteSpace(paragraph))
        {
            return null;
        }

        if (!_rateGuard.TryAcquire(DefaultProfile))
        {
            _logger.LogInformation("LLM style check skipped (rate guard)");
            return null;
        }

        var system = _prompts.GetTemplate("style-check-system");
        var userTemplate = _prompts.GetTemplate("style-check-user");
        var user = userTemplate.Replace("{{paragraph}}", paragraph, StringComparison.Ordinal);

        string raw;
        try
        {
            raw = await _llm.CompleteAsync(
                new LlmCompletionRequest
                {
                    SystemPrompt = system,
                    UserPrompt = user,
                    MaxTokens = Math.Min(120, context.Settings.Llm.MaxTokens + 40),
                    Temperature = 0.1
                },
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM style check failed");
            return null;
        }

        var parsed = StyleCheckJsonParser.TryParse(raw);
        if (parsed is null || !parsed.HasIssue || string.IsNullOrWhiteSpace(parsed.Corrected))
        {
            return null;
        }

        if (string.Equals(parsed.Corrected.Trim(), paragraph.Trim(), StringComparison.Ordinal))
        {
            return null;
        }

        context.GrammarIssueFound = true;
        var corrected = parsed.Corrected.Trim();

        return new Suggestion
        {
            HeaderKey = "Suggestion_GrammarFix",
            Body = corrected,
            Prefix = string.Empty,
            Highlight = corrected,
            Suffix = string.Empty,
            Options = [corrected],
            Kind = SuggestionKind.GrammarFix,
            Level = SuggestionLevel.Grammar,
            AnchorCaret = context.Caret?.CaretRect,
            Monitor = context.Caret?.Monitor
        };
    }
}
