using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Typee.Core.Insertion;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.Insertion;

/// <summary>
/// Full-format clipboard save/restore for paste insertion. Called by <c>TextInserter</c> only.
/// Never logs clipboard contents. No prior clipboard helper exists in this repo.
/// User instruction: "clipboard paste with FULL clipboard save/restore after 300 ms
/// (all formats where possible, not only text); if restore fails, log (no user text)
/// and never leave Typee text in clipboard silently".
/// </summary>
internal sealed class ClipboardGuardian
{
    private readonly ILogger _logger;

    public ClipboardGuardian(ILogger logger)
    {
        _logger = logger;
    }

    public ClipboardSnapshot? TrySave()
    {
        if (!TryOpenClipboard())
        {
            _logger.LogWarning("Clipboard save skipped (OpenClipboard failed, win32={Error})", Marshal.GetLastWin32Error());
            return null;
        }

        try
        {
            var entries = new List<ClipboardFormatEntry>();
            uint format = 0;
            while ((format = EnumClipboardFormats(format)) != 0)
            {
                if (ClipboardFormatCatalog.IsGdiHandleFormat(format))
                {
                    // GDI handle formats need specialized duplication; skip rather than corrupt.
                    _logger.LogDebug("Clipboard save skipped GDI format id={FormatId}", format);
                    continue;
                }

                var handle = GetClipboardData(format);
                if (handle == 0)
                {
                    continue;
                }

                var size = GlobalSize(handle);
                if (size == 0 || size > 16 * 1024 * 1024)
                {
                    _logger.LogDebug("Clipboard save skipped format id={FormatId} size={Size}", format, (ulong)size);
                    continue;
                }

                var source = GlobalLock(handle);
                if (source == 0)
                {
                    continue;
                }

                try
                {
                    var bytes = new byte[(int)size];
                    Marshal.Copy(source, bytes, 0, bytes.Length);
                    entries.Add(new ClipboardFormatEntry(format, bytes));
                }
                finally
                {
                    _ = GlobalUnlock(handle);
                }
            }

            _logger.LogDebug("Clipboard saved formatCount={Count}", entries.Count);
            return new ClipboardSnapshot(entries);
        }
        finally
        {
            _ = CloseClipboard();
        }
    }

    public bool TrySetUnicodeText(string text)
    {
        if (!TryOpenClipboard())
        {
            _logger.LogWarning("Clipboard set skipped (OpenClipboard failed, win32={Error})", Marshal.GetLastWin32Error());
            return false;
        }

        try
        {
            if (!EmptyClipboard())
            {
                _logger.LogWarning("EmptyClipboard failed win32={Error}", Marshal.GetLastWin32Error());
                return false;
            }

            var bytes = (text.Length + 1) * 2;
            var hGlobal = GlobalAlloc(GmemMoveable, (nuint)bytes);
            if (hGlobal == 0)
            {
                return false;
            }

            var ptr = GlobalLock(hGlobal);
            if (ptr == 0)
            {
                _ = GlobalFree(hGlobal);
                return false;
            }

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
                Marshal.WriteInt16(ptr, text.Length * 2, 0);
            }
            finally
            {
                _ = GlobalUnlock(hGlobal);
            }

            if (SetClipboardData(ClipboardFormatCatalog.CfUnicodeText, hGlobal) == 0)
            {
                _ = GlobalFree(hGlobal);
                _logger.LogWarning("SetClipboardData CF_UNICODETEXT failed win32={Error}", Marshal.GetLastWin32Error());
                return false;
            }

            // Ownership transferred to the system — do not GlobalFree.
            return true;
        }
        finally
        {
            _ = CloseClipboard();
        }
    }

    public ClipboardRestoreDecision TryRestoreOrClear(ClipboardSnapshot? snapshot)
    {
        if (snapshot is null || snapshot.Entries.Count == 0)
        {
            var cleared = TryEmpty();
            var decision = ClipboardFormatCatalog.DecideRestore(0, 0, cleared);
            LogRestore(decision, saved: 0, restored: 0);
            return decision;
        }

        if (!TryOpenClipboard())
        {
            _logger.LogWarning("Clipboard restore OpenClipboard failed win32={Error}", Marshal.GetLastWin32Error());
            var cleared = TryEmpty();
            var decision = ClipboardFormatCatalog.DecideRestore(snapshot.Entries.Count, 0, cleared);
            LogRestore(decision, snapshot.Entries.Count, 0);
            return decision;
        }

        var restored = 0;
        try
        {
            if (!EmptyClipboard())
            {
                _logger.LogWarning("Clipboard restore EmptyClipboard failed win32={Error}", Marshal.GetLastWin32Error());
            }

            foreach (var entry in snapshot.Entries)
            {
                var hGlobal = GlobalAlloc(GmemMoveable, (nuint)entry.Data.Length);
                if (hGlobal == 0)
                {
                    continue;
                }

                var ptr = GlobalLock(hGlobal);
                if (ptr == 0)
                {
                    _ = GlobalFree(hGlobal);
                    continue;
                }

                try
                {
                    Marshal.Copy(entry.Data, 0, ptr, entry.Data.Length);
                }
                finally
                {
                    _ = GlobalUnlock(hGlobal);
                }

                if (SetClipboardData(entry.Format, hGlobal) == 0)
                {
                    _ = GlobalFree(hGlobal);
                    _logger.LogDebug("Clipboard restore SetClipboardData failed formatId={FormatId}", entry.Format);
                    continue;
                }

                restored++;
            }
        }
        finally
        {
            _ = CloseClipboard();
        }

        if (restored == 0)
        {
            var cleared = TryEmpty();
            var decision = ClipboardFormatCatalog.DecideRestore(snapshot.Entries.Count, 0, cleared);
            LogRestore(decision, snapshot.Entries.Count, 0);
            return decision;
        }

        var ok = ClipboardFormatCatalog.DecideRestore(snapshot.Entries.Count, restored, emptySucceededAfterFailure: false);
        LogRestore(ok, snapshot.Entries.Count, restored);
        return ok;
    }

    public bool TryEmpty()
    {
        if (!TryOpenClipboard())
        {
            return false;
        }

        try
        {
            return EmptyClipboard();
        }
        finally
        {
            _ = CloseClipboard();
        }
    }

    private void LogRestore(ClipboardRestoreDecision decision, int saved, int restored)
    {
        if (decision is ClipboardRestoreDecision.FullyRestored or ClipboardRestoreDecision.PartiallyRestored)
        {
            _logger.LogInformation(
                "Clipboard restore decision={Decision} savedFormats={Saved} restoredFormats={Restored}",
                decision,
                saved,
                restored);
            return;
        }

        _logger.LogWarning(
            "Clipboard restore decision={Decision} savedFormats={Saved} restoredFormats={Restored}",
            decision,
            saved,
            restored);
    }

    private bool TryOpenClipboard()
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            if (OpenClipboard(0))
            {
                return true;
            }

            Thread.Sleep(15);
        }

        return false;
    }
}

internal sealed class ClipboardSnapshot
{
    public ClipboardSnapshot(IReadOnlyList<ClipboardFormatEntry> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<ClipboardFormatEntry> Entries { get; }
}

internal readonly record struct ClipboardFormatEntry(uint Format, byte[] Data);
