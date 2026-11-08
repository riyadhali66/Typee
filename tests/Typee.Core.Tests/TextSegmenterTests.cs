using Typee.Core.Text;

namespace Typee.Core.Tests;

public sealed class TextSegmenterTests
{
    [Fact]
    public void GetParagraphAt_splits_on_blank_line_breaks()
    {
        const string text = "First para.\n\nSecond para.";
        var secondOffset = text.IndexOf('S', StringComparison.Ordinal);
        Assert.Equal("Second para.", TextSegmenter.GetParagraphAt(text, secondOffset));
    }

    [Fact]
    public void GetLineAt_returns_current_line_without_terminators()
    {
        const string text = "Line one\r\nLine two";
        var offset = text.IndexOf('t', StringComparison.Ordinal);
        Assert.Equal("Line two", TextSegmenter.GetLineAt(text, offset));
    }

    [Fact]
    public void GetPreviousLine_returns_prior_line()
    {
        const string text = "Alpha\nBeta";
        var betaOffset = text.IndexOf('B', StringComparison.Ordinal);
        Assert.Equal("Alpha", TextSegmenter.GetPreviousLine(text, betaOffset));
    }

    [Fact]
    public void GetSentenceAt_stops_at_period()
    {
        const string text = "Hello world. Next";
        var offset = text.IndexOf('w', StringComparison.Ordinal);
        Assert.Equal("Hello world.", TextSegmenter.GetSentenceAt(text, offset));
    }

    [Fact]
    public void GetPreviousLine_at_first_line_returns_null()
    {
        const string text = "Only line";
        Assert.Null(TextSegmenter.GetPreviousLine(text, 3));
    }
}
