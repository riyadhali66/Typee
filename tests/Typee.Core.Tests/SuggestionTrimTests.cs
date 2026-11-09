using Typee.Core.Suggestions;

namespace Typee.Core.Tests;

public sealed class SuggestionTrimTests
{
    [Fact]
    public void TryTrimPrefix_matches_first_char_case_insensitive()
    {
        Assert.True(SuggestionTrim.TryTrimPrefix("Hello", 'h', out var trimmed));
        Assert.Equal("ello", trimmed);
    }

    [Fact]
    public void TryTrimPrefix_rejects_mismatch()
    {
        Assert.False(SuggestionTrim.TryTrimPrefix("Hello", 'x', out var trimmed));
        Assert.Equal("Hello", trimmed);
    }

    [Fact]
    public void TryMapPrintable_maps_letter_and_space()
    {
        Assert.Equal('a', SuggestionTrim.TryMapPrintable(0x41, shift: false));
        Assert.Equal('A', SuggestionTrim.TryMapPrintable(0x41, shift: true));
        Assert.Equal(' ', SuggestionTrim.TryMapPrintable(0x20, shift: false));
        Assert.Null(SuggestionTrim.TryMapPrintable(0x09, shift: false)); // Tab
    }
}
