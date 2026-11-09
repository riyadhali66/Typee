using Typee.Grammar;

namespace Typee.Grammar.Tests;

public sealed class ScriptHeuristicsTests
{
    [Fact]
    public void MostlyLatin_English_IsTrue()
    {
        Assert.True(ScriptHeuristics.IsMostlyLatin("This is a test of the the app"));
    }

    [Fact]
    public void MostlyLatin_Arabic_IsFalse()
    {
        Assert.False(ScriptHeuristics.IsMostlyLatin("هذا نص عربي بالكامل للتجربة"));
    }

    [Fact]
    public void WordSpanAtCaret_FindsCurrentWord()
    {
        const string text = "hello world";
        var (start, length) = ScriptHeuristics.GetWordSpanAtCaret(text, 8); // inside "world"
        Assert.Equal(6, start);
        Assert.Equal(5, length);
    }
}
