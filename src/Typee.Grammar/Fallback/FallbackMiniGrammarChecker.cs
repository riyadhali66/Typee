using System.Text.RegularExpressions;
using Typee.Core.Models;

namespace Typee.Grammar.Fallback;

/// <summary>
/// Tiny offline checker used when harper-ls is missing or disabled:
/// repeated words, double spaces, capital after period.
/// </summary>
public static partial class FallbackMiniGrammarChecker
{
    public static IReadOnlyList<GrammarIssue> Check(string paragraph)
    {
        if (string.IsNullOrEmpty(paragraph))
        {
            return [];
        }

        var issues = new List<GrammarIssue>();
        CollectRepeatedWords(paragraph, issues);
        CollectDoubleSpaces(paragraph, issues);
        CollectCapitalAfterPeriod(paragraph, issues);
        return issues;
    }

    private static void CollectRepeatedWords(string paragraph, List<GrammarIssue> issues)
    {
        foreach (Match match in RepeatedWordRegex().Matches(paragraph))
        {
            if (!match.Success || match.Groups.Count < 2)
            {
                continue;
            }

            var second = match.Groups[2];
            issues.Add(new GrammarIssue
            {
                Start = second.Index,
                Length = second.Length,
                Message = "Repeated word",
                RuleName = "Fallback.RepeatedWords",
                Replacements = [match.Groups[1].Value],
                Kind = GrammarIssueKind.Grammar
            });
        }
    }

    private static void CollectDoubleSpaces(string paragraph, List<GrammarIssue> issues)
    {
        foreach (Match match in DoubleSpaceRegex().Matches(paragraph))
        {
            issues.Add(new GrammarIssue
            {
                Start = match.Index,
                Length = match.Length,
                Message = "Double spaces",
                RuleName = "Fallback.Spaces",
                Replacements = [" "],
                Kind = GrammarIssueKind.Grammar
            });
        }
    }

    private static void CollectCapitalAfterPeriod(string paragraph, List<GrammarIssue> issues)
    {
        foreach (Match match in CapitalAfterPeriodRegex().Matches(paragraph))
        {
            var letter = match.Groups[1];
            var replacement = char.ToUpperInvariant(letter.Value[0]) + letter.Value[1..];
            issues.Add(new GrammarIssue
            {
                Start = letter.Index,
                Length = letter.Length,
                Message = "Capitalize after period",
                RuleName = "Fallback.SentenceCapitalization",
                Replacements = [replacement],
                Kind = GrammarIssueKind.Grammar
            });
        }
    }

    [GeneratedRegex(@"\b(\w+)\s+(\1)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RepeatedWordRegex();

    [GeneratedRegex(@" {2,}", RegexOptions.CultureInvariant)]
    private static partial Regex DoubleSpaceRegex();

    [GeneratedRegex(@"[.!?]\s+([a-z]\w*)", RegexOptions.CultureInvariant)]
    private static partial Regex CapitalAfterPeriodRegex();
}
