using Typee.Core.Abstractions;
using Typee.Core.Models;

namespace Typee.Core.Suggestions.Providers;

public sealed class LocalPhraseSuggestionProvider : ISuggestionProvider
{
    private readonly IPhraseService _phrases;

    public LocalPhraseSuggestionProvider(IPhraseService phrases)
    {
        _phrases = phrases;
    }

    public string Id => "local-phrase";
    public int Priority => SuggestionProviderPriorities.LocalPhrase;

    public Task<Suggestion?> TryProvideAsync(
        SuggestionPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var before = context.Text.TextBeforeCaret;
        if (string.IsNullOrWhiteSpace(before))
        {
            return Task.FromResult<Suggestion?>(null);
        }

        var matches = _phrases.Match(before, maxResults: 1);
        if (matches.Count == 0)
        {
            return Task.FromResult<Suggestion?>(null);
        }

        var phrase = matches[0];
        var completion = DeriveCompletion(before, phrase);
        if (string.IsNullOrEmpty(completion))
        {
            return Task.FromResult<Suggestion?>(null);
        }

        Suggestion suggestion = new()
        {
            HeaderKey = "Suggestion_Completion",
            Body = completion,
            Prefix = string.Empty,
            Highlight = completion,
            Suffix = string.Empty,
            Kind = SuggestionKind.Completion,
            Level = SuggestionLevel.Local,
            AnchorCaret = context.Caret?.CaretRect,
            Monitor = context.Caret?.Monitor
        };

        return Task.FromResult<Suggestion?>(suggestion);
    }

    private static string DeriveCompletion(string textBeforeCaret, Phrase phrase)
    {
        var text = phrase.Text;
        // If the typed prefix already ends with the trigger, suggest the remainder of the phrase.
        if (textBeforeCaret.EndsWith(phrase.TriggerPrefix, StringComparison.OrdinalIgnoreCase)
            && text.StartsWith(phrase.TriggerPrefix, StringComparison.OrdinalIgnoreCase)
            && text.Length > phrase.TriggerPrefix.Length)
        {
            return text[phrase.TriggerPrefix.Length..].TrimStart();
        }

        // Otherwise if the phrase extends what was typed at the end, return the delta.
        for (var len = Math.Min(textBeforeCaret.Length, text.Length); len > 0; len--)
        {
            var suffix = textBeforeCaret[^len..];
            if (text.StartsWith(suffix, StringComparison.OrdinalIgnoreCase)
                && text.Length > len)
            {
                return text[len..];
            }
        }

        return text;
    }
}
