namespace Typee.Grammar.Tests;

/// <summary>
/// Phase 1 keeps the Grammar test project compiling. Harper LSP tests arrive in Phase 6.
/// </summary>
public sealed class Phase1PlaceholderTests
{
    [Fact]
    public void GrammarProject_IsWired()
    {
        Assert.Equal("Typee.Grammar", typeof(Typee.Grammar.GrammarModuleMarker).Namespace);
    }
}
