using Typee.Core.Abstractions;
using Typee.Core.Insertion;
using Typee.Core.Models;
using Typee.Core.Text;

namespace Typee.Core.Suggestions.Providers;

/// <summary>Level 1: Harper/fallback grammar. Sets GrammarIssueFound for downstream L2 gating.</summary>
public sealed class GrammarSuggestionProvider : ISuggestionProvider
{
    private readonly IGrammarChecker _grammar;

    public GrammarSuggestionProvider(IGrammarChecker grammar)
    {
        _grammar = grammar;
    }

    public string Id => "grammar";
    public int Priority => SuggestionProviderPriorities.Grammar;

    public async Task<Suggestion?> TryProvideAsync(
        SuggestionPipelineContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.Settings.Suggestions.Level1GrammarEnabled)
        {
            return null;
        }

        var paragraph = context.Text.CurrentParagraph;
        if (string.IsNullOrWhiteSpace(paragraph))
        {
            return null;
        }

        var caretInParagraph = ResolveCaretInParagraph(context.Text);
        var issues = await _grammar.CheckAsync(paragraph, caretInParagraph, cancellationToken)
            .ConfigureAwait(false);
        context.GrammarIssues = issues;
        context.GrammarIssueFound = issues.Count > 0;

        if (issues.Count == 0)
        {
            return null;
        }

        var issue = issues[0];
        var options = BuildOptions(paragraph, issue);
        if (options.Count == 0)
        {
            return null;
        }

        var primary = options[0];
        SplitAroundChange(paragraph, primary, out var prefix, out var highlight, out var suffix);
        var mode = TextInsertPlanFactory.ParseMode(context.Settings.Suggestions.InsertMode);
        var insert = TextInsertPlanFactory.ForGrammarIssue(context.Text, issue, options, context.Target, mode);

        return new Suggestion
        {
            HeaderKey = "Suggestion_GrammarFix",
            Body = primary,
            Prefix = prefix,
            Highlight = highlight,
            Suffix = suffix,
            Options = options,
            Kind = SuggestionKind.GrammarFix,
            Level = SuggestionLevel.Grammar,
            AnchorCaret = context.Caret?.CaretRect,
            Monitor = context.Caret?.Monitor,
            Insert = insert
        };
    }

    internal static int ResolveCaretInParagraph(TextContext text)
    {
        var paragraph = text.CurrentParagraph ?? string.Empty;
        if (paragraph.Length == 0)
        {
            return 0;
        }

        // Prefer mapping via TextBeforeCaret suffix when possible.
        var before = text.TextBeforeCaret ?? string.Empty;
        if (before.EndsWith(paragraph, StringComparison.Ordinal))
        {
            return paragraph.Length;
        }

        var paraStart = TextSegmenter.FindParagraphStart(
            before.Length >= text.CaretOffset
                ? before
                : before + paragraph,
            Math.Min(text.CaretOffset, before.Length));

        // Fallback: distance from paragraph start within the paragraph string.
        if (before.Length > 0 && paragraph.Length > 0)
        {
            var overlap = 0;
            var max = Math.Min(before.Length, paragraph.Length);
            for (var i = 1; i <= max; i++)
            {
                if (before.EndsWith(paragraph[..i], StringComparison.Ordinal))
                {
                    overlap = i;
                }
            }

            if (overlap > 0)
            {
                return overlap;
            }
        }

        return Math.Clamp(text.CaretOffset - paraStart, 0, paragraph.Length);
    }

    private static IReadOnlyList<string> BuildOptions(string paragraph, GrammarIssue issue)
    {
        var list = new List<string>(3);
        foreach (var replacement in issue.Replacements)
        {
            if (string.IsNullOrEmpty(replacement) && issue.Length == 0)
            {
                continue;
            }

            var start = Math.Clamp(issue.Start, 0, paragraph.Length);
            var length = Math.Clamp(issue.Length, 0, paragraph.Length - start);
            var corrected = string.Concat(paragraph.AsSpan(0, start), replacement, paragraph.AsSpan(start + length));
            if (!string.Equals(corrected, paragraph, StringComparison.Ordinal)
                && !list.Contains(corrected, StringComparer.Ordinal))
            {
                list.Add(corrected);
            }

            if (list.Count >= 3)
            {
                break;
            }
        }

        return list;
    }

    private static void SplitAroundChange(
        string original,
        string corrected,
        out string prefix,
        out string highlight,
        out string suffix)
    {
        var commonPrefix = 0;
        var maxPrefix = Math.Min(original.Length, corrected.Length);
        while (commonPrefix < maxPrefix && original[commonPrefix] == corrected[commonPrefix])
        {
            commonPrefix++;
        }

        var commonSuffix = 0;
        var maxSuffix = Math.Min(original.Length - commonPrefix, corrected.Length - commonPrefix);
        while (commonSuffix < maxSuffix
               && original[original.Length - 1 - commonSuffix] == corrected[corrected.Length - 1 - commonSuffix])
        {
            commonSuffix++;
        }

        prefix = corrected[..commonPrefix];
        highlight = corrected[commonPrefix..(corrected.Length - commonSuffix)];
        suffix = corrected[(corrected.Length - commonSuffix)..];
    }
}
