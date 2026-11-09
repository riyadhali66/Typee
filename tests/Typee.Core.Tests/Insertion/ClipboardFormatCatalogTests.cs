using Typee.Core.Insertion;

namespace Typee.Core.Tests.Insertion;

public sealed class ClipboardFormatCatalogTests
{
    [Fact]
    public void PreferHGlobalFormats_IncludesUnicodeText()
    {
        Assert.Contains(ClipboardFormatCatalog.CfUnicodeText, ClipboardFormatCatalog.PreferHGlobalFormats);
    }

    [Theory]
    [InlineData(ClipboardFormatCatalog.CfBitmap)]
    [InlineData(ClipboardFormatCatalog.CfEnhMetafile)]
    [InlineData(ClipboardFormatCatalog.CfPalette)]
    public void IsGdiHandleFormat_TrueForGdi(uint format)
    {
        Assert.True(ClipboardFormatCatalog.IsGdiHandleFormat(format));
    }

    [Fact]
    public void IsGdiHandleFormat_FalseForUnicodeText()
    {
        Assert.False(ClipboardFormatCatalog.IsGdiHandleFormat(ClipboardFormatCatalog.CfUnicodeText));
    }

    [Fact]
    public void DecideRestore_FullyRestored_WhenAllFormatsReturn()
    {
        Assert.Equal(
            ClipboardRestoreDecision.FullyRestored,
            ClipboardFormatCatalog.DecideRestore(savedFormatCount: 3, restoredFormatCount: 3, emptySucceededAfterFailure: false));
    }

    [Fact]
    public void DecideRestore_Partial_WhenSomeFormatsReturn()
    {
        Assert.Equal(
            ClipboardRestoreDecision.PartiallyRestored,
            ClipboardFormatCatalog.DecideRestore(savedFormatCount: 3, restoredFormatCount: 1, emptySucceededAfterFailure: false));
    }

    [Fact]
    public void DecideRestore_ClearsTypeeText_WhenRestoreFailsButEmptyWorks()
    {
        Assert.Equal(
            ClipboardRestoreDecision.ClearedTypeeText,
            ClipboardFormatCatalog.DecideRestore(savedFormatCount: 2, restoredFormatCount: 0, emptySucceededAfterFailure: true));
    }

    [Fact]
    public void DecideRestore_FailedLeftTypeeText_WhenEmptyAlsoFails()
    {
        Assert.Equal(
            ClipboardRestoreDecision.FailedLeftTypeeText,
            ClipboardFormatCatalog.DecideRestore(savedFormatCount: 2, restoredFormatCount: 0, emptySucceededAfterFailure: false));
    }

    [Fact]
    public void DecideRestore_NoSaved_StillClears()
    {
        Assert.Equal(
            ClipboardRestoreDecision.ClearedTypeeText,
            ClipboardFormatCatalog.DecideRestore(savedFormatCount: 0, restoredFormatCount: 0, emptySucceededAfterFailure: true));
    }
}
