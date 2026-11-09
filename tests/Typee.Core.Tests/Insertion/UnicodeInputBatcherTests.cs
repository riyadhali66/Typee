using Typee.Core.Insertion;

namespace Typee.Core.Tests.Insertion;

/// <summary>
/// Behaviour under test: UTF-16 code-unit batching for KEYEVENTF_UNICODE (Arabic + emoji).
/// Module: Typee.Core.Insertion.UnicodeInputBatcher. No prior tests. User: "Unit tests for
/// pure parts (… unicode batching edge cases)".
/// </summary>
public sealed class UnicodeInputBatcherTests
{
    [Fact]
    public void ToCodeUnits_Empty_ReturnsEmpty()
    {
        Assert.Empty(UnicodeInputBatcher.ToCodeUnits(string.Empty));
    }

    [Fact]
    public void ToCodeUnits_AsciiAndArabic_PreservesOrder()
    {
        const string text = "abمرحبا";
        var units = UnicodeInputBatcher.ToCodeUnits(text);
        Assert.Equal(text.Length, units.Count);
        Assert.Equal(text, new string(units.ToArray()));
    }

    [Fact]
    public void ToCodeUnits_EmojiSurrogatePair_KeepsBothUnits()
    {
        const string emoji = "😀"; // U+1F600
        var units = UnicodeInputBatcher.ToCodeUnits(emoji);
        Assert.Equal(2, units.Count);
        Assert.True(char.IsHighSurrogate(units[0]));
        Assert.True(char.IsLowSurrogate(units[1]));
        Assert.True(UnicodeInputBatcher.IsSurrogatePairAt(units, 0));
    }

    [Fact]
    public void SplitBatches_DoesNotSplitSurrogatePair()
    {
        var text = "a😀b";
        var units = UnicodeInputBatcher.ToCodeUnits(text);
        var batches = UnicodeInputBatcher.SplitBatches(units, batchSize: 2);

        Assert.True(batches.Count >= 2);
        foreach (var batch in batches)
        {
            if (batch.Count >= 1)
            {
                var last = batch.Array![batch.Offset + batch.Count - 1];
                if (char.IsHighSurrogate(last) && batch.Offset + batch.Count < units.Count)
                {
                    Assert.Fail("High surrogate left alone at end of batch");
                }
            }
        }

        var rejoined = string.Concat(batches.SelectMany(b => b));
        Assert.Equal(text, rejoined);
    }

    [Fact]
    public void SplitBatches_RespectsBatchSize_ForBmp()
    {
        var units = UnicodeInputBatcher.ToCodeUnits(new string('x', 10));
        var batches = UnicodeInputBatcher.SplitBatches(units, batchSize: 4);
        Assert.Equal(3, batches.Count);
        Assert.Equal(4, batches[0].Count);
        Assert.Equal(4, batches[1].Count);
        Assert.Equal(2, batches[2].Count);
    }

    [Fact]
    public void CountInputEvents_IsTwoPerUnit()
    {
        Assert.Equal(6, UnicodeInputBatcher.CountInputEvents(3));
    }
}
