using Typee.Core.Insertion;
using Typee.Core.Models;

namespace Typee.Core.Tests.Insertion;

public sealed class DocumentSpanMapperTests
{
    [Fact]
    public void TryMapParagraphSpan_MapsIssueIntoDocumentCoords()
    {
        // Document: "Hi.\nteh cat" caret at end. Paragraph "teh cat", issue "teh" at 0..3.
        var text = new TextContext
        {
            TextBeforeCaret = "Hi.\nteh cat",
            CurrentParagraph = "teh cat",
            CaretOffset = "Hi.\nteh cat".Length
        };

        Assert.True(DocumentSpanMapper.TryMapParagraphSpan(text, 0, 3, out var span, out var expected));
        Assert.Equal("teh", expected);
        Assert.Equal(4, span.Start); // after "Hi.\n"
        Assert.Equal(3, span.Length);
    }

    [Fact]
    public void ExtractReplacement_PullsChangedFragment()
    {
        const string original = "teh cat sat";
        const string corrected = "the cat sat";
        var replacement = DocumentSpanMapper.ExtractReplacement(original, corrected, issueStart: 0, issueLength: 3);
        Assert.Equal("the", replacement);
    }

    [Fact]
    public void ExtractReplacement_HandlesLengthChange()
    {
        const string original = "colour";
        const string corrected = "color";
        var replacement = DocumentSpanMapper.ExtractReplacement(original, corrected, 0, original.Length);
        Assert.Equal("color", replacement);
    }

    [Fact]
    public void BuildContextSuffix_Truncates()
    {
        var suffix = DocumentSpanMapper.BuildContextSuffix(new string('a', 100), maxChars: 10);
        Assert.Equal(10, suffix.Length);
        Assert.Equal(new string('a', 10), suffix);
    }

    [Fact]
    public void TextInsertPlanFactory_ForGrammarIssue_BuildsReplacePlan()
    {
        var text = new TextContext
        {
            TextBeforeCaret = "teh",
            CurrentParagraph = "teh",
            CaretOffset = 3
        };
        var issue = new GrammarIssue { Start = 0, Length = 3, Replacements = ["the"] };
        var plan = TextInsertPlanFactory.ForGrammarIssue(
            text,
            issue,
            ["the"],
            new FocusIdentity(42, "1.2"),
            TextInsertMode.SendInput);

        Assert.NotNull(plan);
        Assert.Equal(TextInsertAction.ReplaceSpan, plan!.Action);
        Assert.Equal("the", plan.InsertText);
        Assert.Equal("teh", plan.ExpectedSpanText);
        Assert.Equal(0, plan.ReplaceSpan!.Value.Start);
        Assert.Equal(3, plan.ReplaceSpan.Value.Length);
    }

    [Fact]
    public void TextInsertPlanFactory_ForAppend_StoresContextSuffix()
    {
        var text = new TextContext
        {
            TextBeforeCaret = "hello there friend",
            CurrentParagraph = "hello there friend",
            CaretOffset = 18
        };
        var plan = TextInsertPlanFactory.ForAppend(text, " forever", new FocusIdentity(1, ""), TextInsertMode.ClipboardPaste);
        Assert.Equal(TextInsertAction.AppendAtCaret, plan.Action);
        Assert.Equal(" forever", plan.InsertText);
        Assert.Equal(TextInsertMode.ClipboardPaste, plan.PreferredMode);
        Assert.False(string.IsNullOrEmpty(plan.ExpectedContextSuffix));
        Assert.EndsWith(plan.ExpectedContextSuffix!, text.TextBeforeCaret, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SendInput", TextInsertMode.SendInput)]
    [InlineData("Clipboard", TextInsertMode.ClipboardPaste)]
    [InlineData("ClipboardPaste", TextInsertMode.ClipboardPaste)]
    public void ParseMode_RecognizesValues(string raw, TextInsertMode expected)
    {
        Assert.Equal(expected, TextInsertPlanFactory.ParseMode(raw));
    }
}
