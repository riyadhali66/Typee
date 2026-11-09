using Typee.Core.Models;

namespace Typee.Core.Insertion;

/// <summary>Builds <see cref="TextInsertPlan"/> values for suggestion providers (pure).</summary>
public static class TextInsertPlanFactory
{
    public static TextInsertPlan? ForGrammarIssue(
        TextContext text,
        GrammarIssue issue,
        IReadOnlyList<string> correctedParagraphOptions,
        FocusIdentity target,
        TextInsertMode mode)
    {
        if (correctedParagraphOptions.Count == 0)
        {
            return null;
        }

        var paragraph = text.CurrentParagraph ?? string.Empty;
        if (!DocumentSpanMapper.TryMapParagraphSpan(
                text,
                issue.Start,
                issue.Length,
                out var span,
                out var expected))
        {
            return null;
        }

        var insertOptions = new string[correctedParagraphOptions.Count];
        for (var i = 0; i < correctedParagraphOptions.Count; i++)
        {
            insertOptions[i] = DocumentSpanMapper.ExtractReplacement(
                paragraph,
                correctedParagraphOptions[i],
                issue.Start,
                issue.Length);
        }

        return new TextInsertPlan
        {
            Action = TextInsertAction.ReplaceSpan,
            InsertText = insertOptions[0],
            InsertOptions = insertOptions,
            ReplaceSpan = span,
            ExpectedSpanText = expected,
            Target = target,
            PreferredMode = mode
        };
    }

    public static TextInsertPlan? ForWholeParagraphReplace(
        TextContext text,
        string correctedParagraph,
        FocusIdentity target,
        TextInsertMode mode)
    {
        if (!DocumentSpanMapper.TryMapWholeParagraph(text, out var span, out var expected))
        {
            return null;
        }

        return new TextInsertPlan
        {
            Action = TextInsertAction.ReplaceSpan,
            InsertText = correctedParagraph,
            InsertOptions = [correctedParagraph],
            ReplaceSpan = span,
            ExpectedSpanText = expected,
            Target = target,
            PreferredMode = mode
        };
    }

    public static TextInsertPlan ForAppend(
        TextContext text,
        string completion,
        FocusIdentity target,
        TextInsertMode mode)
    {
        return new TextInsertPlan
        {
            Action = TextInsertAction.AppendAtCaret,
            InsertText = completion,
            ExpectedContextSuffix = DocumentSpanMapper.BuildContextSuffix(text.TextBeforeCaret),
            Target = target,
            PreferredMode = mode
        };
    }

    public static TextInsertMode ParseMode(string? mode) =>
        string.Equals(mode, "Clipboard", StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, "ClipboardPaste", StringComparison.OrdinalIgnoreCase)
            ? TextInsertMode.ClipboardPaste
            : TextInsertMode.SendInput;
}
