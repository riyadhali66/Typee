using Typee.Grammar.Lsp;

namespace Typee.Grammar.Tests;

public sealed class LspPositionConverterTests
{
    [Fact]
    public void OffsetToPosition_SimpleAscii()
    {
        const string text = "ab\ncd";
        Assert.Equal((0, 0), LspPositionConverter.OffsetToPosition(text, 0));
        Assert.Equal((0, 2), LspPositionConverter.OffsetToPosition(text, 2));
        Assert.Equal((1, 0), LspPositionConverter.OffsetToPosition(text, 3));
        Assert.Equal((1, 2), LspPositionConverter.OffsetToPosition(text, 5));
    }

    [Fact]
    public void RoundTrip_CrLf()
    {
        const string text = "one\r\ntwo";
        var (line, character) = LspPositionConverter.OffsetToPosition(text, 5); // 't'
        Assert.Equal(1, line);
        Assert.Equal(0, character);
        Assert.Equal(5, LspPositionConverter.PositionToOffset(text, line, character));
    }

    [Fact]
    public void SurrogatePair_CountsAsTwoUtf16Units()
    {
        // U+1F600 😀 is one rune / two UTF-16 code units
        const string text = "a😀b";
        Assert.Equal(4, text.Length);

        var (line, character) = LspPositionConverter.OffsetToPosition(text, 3); // before 'b'
        Assert.Equal(0, line);
        Assert.Equal(3, character);
        Assert.Equal(3, LspPositionConverter.PositionToOffset(text, 0, 3));
        Assert.Equal(4, LspPositionConverter.PositionToOffset(text, 0, 4));
    }

    [Theory]
    [InlineData("hello", 0, 0, 0)]
    [InlineData("hello", 0, 5, 5)]
    [InlineData("a\nb\nc", 2, 1, 5)]
    public void PositionToOffset_Matches(string text, int line, int character, int expectedOffset)
    {
        Assert.Equal(expectedOffset, LspPositionConverter.PositionToOffset(text, line, character));
    }
}
