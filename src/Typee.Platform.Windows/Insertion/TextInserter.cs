using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Insertion;
using Typee.Core.Models;
using Typee.Platform.Windows.Process;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.Insertion;

/// <summary>
/// Phase 8 text insertion: SendInput UNICODE batches and clipboard paste with full restore.
/// Never logs document/clipboard text. Never called from the hook callback.
/// </summary>
public sealed class TextInserter : ITextInserter
{
    public static readonly TimeSpan ClipboardRestoreDelay = TimeSpan.FromMilliseconds(300);

    private readonly ITextContextReader _textReader;
    private readonly IFocusSnapshotService _focusSnapshots;
    private readonly ProcessInfoService _guard;
    private readonly ILogger<TextInserter> _logger;
    private readonly ClipboardGuardian _clipboard;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TextInserter(
        ITextContextReader textReader,
        IFocusSnapshotService focusSnapshots,
        ProcessInfoService guard,
        ILogger<TextInserter> logger)
    {
        _textReader = textReader;
        _focusSnapshots = focusSnapshots;
        _guard = guard;
        _logger = logger;
        _clipboard = new ClipboardGuardian(logger);
    }

    public Task<bool> InsertAsync(string text, TextInsertMode mode, CancellationToken cancellationToken = default)
        => RunExclusiveAsync(() => InsertCoreGuarded(text, mode), cancellationToken);

    public Task<bool> ReplaceSpanAsync(TextReplaceRequest request, CancellationToken cancellationToken = default)
        => RunExclusiveAsync(() => ReplaceSpanCore(request), cancellationToken);

    public Task<bool> ApplyPlanAsync(TextInsertPlan plan, string insertText, CancellationToken cancellationToken = default)
        => RunExclusiveAsync(() => ApplyPlanCore(plan, insertText), cancellationToken);

    private async Task<bool> RunExclusiveAsync(Func<bool> action, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(action, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool InsertCoreGuarded(string text, TextInsertMode mode)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        if (_guard.GetSkipReason() is { } skip)
        {
            _logger.LogInformation("Insert aborted (skip={Skip})", skip);
            return false;
        }

        return InsertCore(text, mode);
    }

    private bool ApplyPlanCore(TextInsertPlan plan, string insertText)
    {
        if (string.IsNullOrEmpty(insertText))
        {
            return true;
        }

        if (_guard.GetSkipReason() is { } skip)
        {
            _logger.LogInformation("Insert plan aborted (skip={Skip})", skip);
            return false;
        }

        var actualFocus = _focusSnapshots.CaptureIdentity() ?? FocusIdentity.Empty;
        string? spanText = null;
        string? before = null;

        if (plan.Action == TextInsertAction.ReplaceSpan && plan.ReplaceSpan is { } span)
        {
            spanText = _textReader.TryReadSpanText(span);
        }
        else
        {
            before = _textReader.TryRead(64)?.TextBeforeCaret;
        }

        var decision = InsertVerification.VerifyPlan(plan, actualFocus, spanText, before);
        if (decision != InsertVerificationDecision.Allow)
        {
            _logger.LogInformation("Insert plan aborted silently (reason={Reason})", decision);
            return false;
        }

        if (plan.Action == TextInsertAction.ReplaceSpan && plan.ReplaceSpan is { } replaceSpan)
        {
            return ReplaceSpanCore(new TextReplaceRequest
            {
                Span = replaceSpan,
                ExpectedOriginal = plan.ExpectedSpanText ?? string.Empty,
                Replacement = insertText,
                Target = plan.Target,
                Mode = plan.PreferredMode
            });
        }

        return InsertCore(insertText, plan.PreferredMode);
    }

    private bool ReplaceSpanCore(TextReplaceRequest request)
    {
        if (_guard.GetSkipReason() is { } skip)
        {
            _logger.LogInformation("Replace aborted (skip={Skip})", skip);
            return false;
        }

        var actualFocus = _focusSnapshots.CaptureIdentity() ?? FocusIdentity.Empty;
        var focusDecision = InsertVerification.VerifyFocus(request.Target, actualFocus);
        if (focusDecision != InsertVerificationDecision.Allow)
        {
            _logger.LogInformation("Replace aborted silently (reason={Reason})", focusDecision);
            return false;
        }

        var actualSpan = _textReader.TryReadSpanText(request.Span);
        var spanDecision = InsertVerification.VerifySpanText(request.ExpectedOriginal, actualSpan);
        if (spanDecision != InsertVerificationDecision.Allow)
        {
            _logger.LogInformation("Replace aborted silently (reason={Reason})", spanDecision);
            return false;
        }

        if (!TrySelectSpan(request.Span, request.ExpectedOriginal))
        {
            _logger.LogInformation("Replace aborted (select failed)");
            return false;
        }

        if (!InsertCore(request.Replacement, request.Mode))
        {
            return false;
        }

        var restoreOffset = request.RestoreCaretOffset >= 0
            ? request.RestoreCaretOffset
            : request.Span.Start + request.Replacement.Length;
        if (!_textReader.TrySetCaretOffset(restoreOffset))
        {
            _logger.LogDebug("Caret restore after replace unavailable (offset={Offset})", restoreOffset);
        }

        return true;
    }

    private bool TrySelectSpan(TextSpan span, string expectedOriginal)
    {
        if (_textReader.TrySelect(span))
        {
            return true;
        }

        _logger.LogDebug("UIA Select failed; trying Shift+arrow fallback ({Start},{Length})", span.Start, span.Length);

        var context = _textReader.TryRead(8);
        if (context is null)
        {
            return false;
        }

        var delta = span.Start - context.CaretOffset;
        if (!SendInputUnicode.TryMoveCaretByArrows(delta, _logger)
            || !SendInputUnicode.TryShiftArrows(span.Length, right: true, _logger))
        {
            return false;
        }

        // Re-verify: prefer UIA select again; otherwise confirm selection offsets + span text.
        if (_textReader.TrySelect(span))
        {
            return true;
        }

        var after = _textReader.TryRead(Math.Max(64, span.Length + 8));
        if (after?.Selection is { } sel
            && sel.Start == span.Start
            && sel.Length == span.Length)
        {
            var selectedText = _textReader.TryReadSpanText(span);
            return InsertVerification.VerifySpanText(expectedOriginal, selectedText)
                   == InsertVerificationDecision.Allow;
        }

        _logger.LogInformation("Shift+arrow select could not be verified; aborting replace");
        return false;
    }

    private bool InsertCore(string text, TextInsertMode mode)
    {
        try
        {
            return mode == TextInsertMode.ClipboardPaste
                ? InsertViaClipboard(text)
                : SendInputUnicode.TrySend(text, _logger);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Insert failed");
            return false;
        }
    }

    private bool InsertViaClipboard(string text)
    {
        // Called under _gate — save/paste/restore are serialized (including the 300 ms wait).
        var saved = _clipboard.TrySave();
        if (!_clipboard.TrySetUnicodeText(text))
        {
            if (saved is not null)
            {
                _ = _clipboard.TryRestoreOrClear(saved);
            }
            else
            {
                _ = _clipboard.TryEmpty();
            }

            return false;
        }

        var pasted = SendInputUnicode.TryChord((ushort)VkControl, (ushort)VkV, _logger);
        try
        {
            Thread.Sleep(ClipboardRestoreDelay);
            _ = _clipboard.TryRestoreOrClear(saved);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Clipboard restore failed");
            _ = _clipboard.TryEmpty();
        }

        if (!pasted)
        {
            _logger.LogWarning("Clipboard paste chord failed; restore attempted");
        }

        return pasted;
    }
}
