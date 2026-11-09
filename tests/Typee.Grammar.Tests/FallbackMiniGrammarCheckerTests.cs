using Typee.Grammar.Fallback;

namespace Typee.Grammar.Tests;

public sealed class FallbackMiniGrammarCheckerTests
{
    [Fact]
    public void Detects_RepeatedWords()
    {
        var issues = FallbackMiniGrammarChecker.Check("the the app");
        Assert.Contains(issues, i => i.RuleName == "Fallback.RepeatedWords");
    }

    [Fact]
    public void Detects_DoubleSpaces()
    {
        var issues = FallbackMiniGrammarChecker.Check("a  test");
        Assert.Contains(issues, i => i.RuleName == "Fallback.Spaces" && i.Replacements.Contains(" "));
    }

    [Fact]
    public void Detects_CapitalAfterPeriod()
    {
        var issues = FallbackMiniGrammarChecker.Check("Done. next");
        Assert.Contains(issues, i => i.RuleName == "Fallback.SentenceCapitalization");
    }

    [Fact]
    public void SampleAcceptancePhrase_FindsIssues()
    {
        var issues = FallbackMiniGrammarChecker.Check("this is a  test test of the the app");
        Assert.True(issues.Count >= 2);
        Assert.Contains(issues, i => i.RuleName == "Fallback.Spaces");
        Assert.Contains(issues, i => i.RuleName == "Fallback.RepeatedWords");
    }
}
