namespace Typee.Grammar.Tests;

public sealed class Phase1PlaceholderTests
{
    [Fact]
    public void GrammarProject_IsWired()
    {
        Assert.Equal("Phase 6 — Harper grammar", Typee.Grammar.GrammarModuleMarker.PhaseStatus);
    }
}
