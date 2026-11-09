namespace Typee.Core.Insertion;

/// <summary>
/// Known Win32 clipboard format IDs and pure helpers for restore policy.
/// Does not touch the clipboard; Platform uses this for enumeration ordering / decisions.
/// </summary>
public static class ClipboardFormatCatalog
{
    public const uint CfText = 1;
    public const uint CfBitmap = 2;
    public const uint CfMetafilePict = 3;
    public const uint CfSylk = 4;
    public const uint CfDif = 5;
    public const uint CfTiff = 6;
    public const uint CfOemText = 7;
    public const uint CfDib = 8;
    public const uint CfPalette = 9;
    public const uint CfPenData = 10;
    public const uint CfRiff = 11;
    public const uint CfWave = 12;
    public const uint CfUnicodeText = 13;
    public const uint CfEnhMetafile = 14;
    public const uint CfHdrop = 15;
    public const uint CfLocale = 16;
    public const uint CfDibV5 = 17;

    /// <summary>Standard formats we attempt to duplicate as HGLOBAL memory blocks.</summary>
    public static readonly uint[] PreferHGlobalFormats =
    [
        CfUnicodeText,
        CfText,
        CfOemText,
        CfLocale,
        CfDib,
        CfDibV5,
        CfHdrop,
        CfRiff,
        CfWave
    ];

    /// <summary>
    /// Formats that use GDI/handle semantics and must not be blindly GlobalFree'd as HGLOBAL.
    /// Still enumerated for best-effort restore via specialized duplication in Platform.
    /// </summary>
    public static bool IsGdiHandleFormat(uint format) =>
        format is CfBitmap or CfMetafilePict or CfEnhMetafile or CfPalette or CfPenData;

    /// <summary>
    /// Decides restore outcome after a paste. Never includes clipboard contents.
    /// </summary>
    public static ClipboardRestoreDecision DecideRestore(
        int savedFormatCount,
        int restoredFormatCount,
        bool emptySucceededAfterFailure)
    {
        if (savedFormatCount == 0)
        {
            // Nothing to restore — clearing Typee text is still required.
            return emptySucceededAfterFailure
                ? ClipboardRestoreDecision.ClearedTypeeText
                : ClipboardRestoreDecision.FailedLeftTypeeText;
        }

        if (restoredFormatCount > 0 && restoredFormatCount >= savedFormatCount)
        {
            return ClipboardRestoreDecision.FullyRestored;
        }

        if (restoredFormatCount > 0)
        {
            return ClipboardRestoreDecision.PartiallyRestored;
        }

        return emptySucceededAfterFailure
            ? ClipboardRestoreDecision.ClearedTypeeText
            : ClipboardRestoreDecision.FailedLeftTypeeText;
    }
}

public enum ClipboardRestoreDecision
{
    FullyRestored = 0,
    PartiallyRestored = 1,
    ClearedTypeeText = 2,
    FailedLeftTypeeText = 3
}
