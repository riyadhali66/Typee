using Typee.Core.Llm;

namespace Typee.Core.Tests;

public sealed class StyleCheckJsonParserTests
{
    [Fact]
    public void Parses_StrictJson()
    {
        var result = StyleCheckJsonParser.TryParse(
            """{"hasIssue":true,"corrected":"Hello world.","explanation":"Capitalize"}""");

        Assert.NotNull(result);
        Assert.True(result!.HasIssue);
        Assert.Equal("Hello world.", result.Corrected);
        Assert.Equal("Capitalize", result.Explanation);
    }

    [Fact]
    public void Parses_MarkdownFencedJson()
    {
        var raw = """
                  ```json
                  {"hasIssue":false,"corrected":"","explanation":""}
                  ```
                  """;
        var result = StyleCheckJsonParser.TryParse(raw);
        Assert.NotNull(result);
        Assert.False(result!.HasIssue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{broken")]
    [InlineData("{\"corrected\":\"x\"}")]
    public void Malformed_ReturnsNull(string raw)
    {
        Assert.Null(StyleCheckJsonParser.TryParse(raw));
    }
}
