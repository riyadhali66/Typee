using Typee.Core.Models;
using Typee.Core.Suggestions.Providers;
using Typee.Core.Text;

namespace Typee.Core.Insertion;

/// <summary>Maps paragraph-local offsets to document <see cref="TextSpan"/> values (pure).</summary>
public static class DocumentSpanMapper
{
    public const int DefaultContextSuffixChars = 32;

    /// <summary>
    /// Called from grammar/style suggestion providers when attaching <see cref="TextInsertPlan"/>,
    /// and from unit tests.
    /// </summary>
    public static bool TryMapParagraphSpan(
        TextContext text,
        int paragraphStart,
        int paragraphLength,
        out TextSpan documentSpan,
        out string expectedText)
    {
        documentSpan = default;
        expectedText = string.Empty;

        var paragraph = text.CurrentParagraph ?? string.Empty;
        if (paragraph.Length == 0)
        {
            return false;
        }

        var start = Math.Clamp(paragraphStart, 0, paragraph.Length);
        var length = Math.Clamp(paragraphLength, 0, paragraph.Length - start);
        expectedText = paragraph.Substring(start, length);

        var caretInParagraph = GrammarSuggestionProvider.ResolveCaretInParagraph(text);
        var paragraphDocStart = text.CaretOffset - caretInParagraph;
        if (paragraphDocStart < 0)
        {
            return false;
        }

        documentSpan = new TextSpan(paragraphDocStart + start, length);
        return true;
    }

    public static bool TryMapWholeParagraph(
        TextContext text,
        out TextSpan documentSpan,
        out string expectedText)
    {
        var paragraph = text.CurrentParagraph ?? string.Empty;
        return TryMapParagraphSpan(text, 0, paragraph.Length, out documentSpan, out expectedText);
    }

    public static string BuildContextSuffix(string? textBeforeCaret, int maxChars = DefaultContextSuffixChars)
    {
        if (string.IsNullOrEmpty(textBeforeCaret) || maxChars <= 0)
        {
            return string.Empty;
        }

        return textBeforeCaret.Length <= maxChars
            ? textBeforeCaret
            : textBeforeCaret[^maxChars..];
    }

    /// <summary>
    /// Extracts the replacement fragment for a grammar option that is a full corrected paragraph.
    /// </summary>
    public static string ExtractReplacement(string originalParagraph, string correctedParagraph, int issueStart, int issueLength)
    {
        var start = Math.Clamp(issueStart, 0, originalParagraph.Length);
        var length = Math.Clamp(issueLength, 0, originalParagraph.Length - start);
        var suffixLen = originalParagraph.Length - start - length;
        if (suffixLen < 0 || correctedParagraph.Length < start + suffixLen)
        {
            return correctedParagraph;
        }

        var replacementEnd = correctedParagraph.Length - suffixLen;
        if (replacementEnd < start)
        {
            return correctedParagraph;
        }

        return correctedParagraph[start..replacementEnd];
    }

    public static int EstimateParagraphDocumentStart(TextContext text)
    {
        var caretInParagraph = GrammarSuggestionProvider.ResolveCaretInParagraph(text);
        return Math.Max(0, text.CaretOffset - caretInParagraph);
    }

    public static TextSpan? TryFindParagraphBreakAwareStart(string fullBeforeAndParagraph, int caretOffset)
    {
        if (string.IsNullOrEmpty(fullBeforeAndParagraph))
        {
            return null;
        }

        var clamped = Math.Min(caretOffset, fullBeforeAndParagraph.Length);
        var start = TextSegmenter.FindParagraphStart(fullBeforeAndParagraph, clamped);
        var end = TextSegmenter.FindParagraphEnd(fullBeforeAndParagraph, clamped);
        return new TextSpan(start, Math.Max(0, end - start));
    }
}
