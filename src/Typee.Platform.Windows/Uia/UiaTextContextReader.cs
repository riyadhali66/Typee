using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Core.Text;
using Typee.Platform.Windows.Process;
using Interop.UIAutomationClient;

namespace Typee.Platform.Windows.Uia;

public sealed class UiaTextContextReader : ITextContextReader
{
    private readonly UiaThread _uiaThread;
    private readonly ProcessInfoService _guard;
    private readonly ILogger<UiaTextContextReader> _logger;

    public UiaTextContextReader(
        UiaThread uiaThread,
        ProcessInfoService guard,
        ILogger<UiaTextContextReader> logger)
    {
        _uiaThread = uiaThread;
        _guard = guard;
        _logger = logger;
    }

    public TextContext? TryRead(int maxContextChars)
    {
        if (_guard.GetSkipReason() is not null)
        {
            return null;
        }

        return _uiaThread.Invoke(automation => ReadOnThread(automation, maxContextChars));
    }

    public ScreenRect? TryGetCharacterRangeScreenRect(TextSpan span)
    {
        if (_guard.GetSkipReason() is not null)
        {
            return null;
        }

        return _uiaThread.Invoke(automation =>
        {
            if (_guard.TryGetSafeFocusedElement(automation, out var element) is not null || element is null)
            {
                return null;
            }

            var pattern = UiaPatternHelper.TryGetTextPattern(element);
            if (pattern is null)
            {
                return null;
            }

            try
            {
                var range = CreateRange(pattern, span);
                return range is null ? null : UiaRectHelper.FirstBoundingRect(range.GetBoundingRectangles());
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "TryGetCharacterRangeScreenRect failed");
                return null;
            }
        });
    }

    public bool TrySelect(TextSpan span)
    {
        if (_guard.GetSkipReason() is not null)
        {
            return false;
        }

        return _uiaThread.Invoke(automation =>
        {
            if (_guard.TryGetSafeFocusedElement(automation, out var element) is not null || element is null)
            {
                return false;
            }

            var pattern = UiaPatternHelper.TryGetTextPattern(element);
            if (pattern is null)
            {
                return false;
            }

            try
            {
                var range = CreateRange(pattern, span);
                if (range is null)
                {
                    return false;
                }

                range.Select();
                _logger.LogDebug("Selected character range ({Start},{Length})", span.Start, span.Length);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "TrySelect failed");
                return false;
            }
        });
    }

    public string? TryReadSpanText(TextSpan span)
    {
        if (_guard.GetSkipReason() is not null)
        {
            return null;
        }

        return _uiaThread.Invoke(automation =>
        {
            if (_guard.TryGetSafeFocusedElement(automation, out var element) is not null || element is null)
            {
                return null;
            }

            var pattern = UiaPatternHelper.TryGetTextPattern(element);
            if (pattern is null)
            {
                return null;
            }

            try
            {
                var range = CreateRange(pattern, span);
                if (range is null)
                {
                    return null;
                }

                // Never log the returned text.
                return range.GetText(span.Length < 0 ? -1 : span.Length) ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "TryReadSpanText failed");
                return null;
            }
        });
    }

    public bool TrySetCaretOffset(int caretOffset)
    {
        if (caretOffset < 0 || _guard.GetSkipReason() is not null)
        {
            return false;
        }

        return _uiaThread.Invoke(automation =>
        {
            if (_guard.TryGetSafeFocusedElement(automation, out var element) is not null || element is null)
            {
                return false;
            }

            var pattern = UiaPatternHelper.TryGetTextPattern(element);
            if (pattern is null)
            {
                return false;
            }

            try
            {
                var range = CreateRange(pattern, new TextSpan(caretOffset, 0));
                if (range is null)
                {
                    return false;
                }

                range.Select();
                _logger.LogDebug("Set caret offset={Offset}", caretOffset);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "TrySetCaretOffset failed");
                return false;
            }
        });
    }

    private TextContext? ReadOnThread(CUIAutomation8 automation, int maxContextChars)
    {
        if (_guard.TryGetSafeFocusedElement(automation, out var element) is not null || element is null)
        {
            return null;
        }

        var pattern = UiaPatternHelper.TryGetTextPattern(element);
        if (pattern is null)
        {
            return null;
        }

        try
        {
            var document = pattern.DocumentRange;
            var fullText = document.GetText(-1) ?? string.Empty;

            var caretOffset = ResolveCaretOffset(pattern, document);
            if (caretOffset < 0)
            {
                caretOffset = fullText.Length;
            }

            var beforeStart = Math.Max(0, caretOffset - Math.Max(1, maxContextChars));
            var textBefore = fullText[beforeStart..caretOffset];

            TextSpan? selection = null;
            var selectionArrays = pattern.GetSelection();
            if (selectionArrays.Length > 0)
            {
                var sel = selectionArrays.GetElement(0);
                var start = sel.CompareEndpoints(
                    TextPatternRangeEndpoint.TextPatternRangeEndpoint_Start,
                    document,
                    TextPatternRangeEndpoint.TextPatternRangeEndpoint_Start);
                var end = sel.CompareEndpoints(
                    TextPatternRangeEndpoint.TextPatternRangeEndpoint_End,
                    document,
                    TextPatternRangeEndpoint.TextPatternRangeEndpoint_Start);
                if (end > start)
                {
                    selection = new TextSpan(start, end - start);
                }
            }

            return new TextContext
            {
                TextBeforeCaret = textBefore,
                CurrentSentence = TextSegmenter.GetSentenceAt(fullText, caretOffset),
                CurrentParagraph = TextSegmenter.GetParagraphAt(fullText, caretOffset),
                CurrentLine = TextSegmenter.GetLineAt(fullText, caretOffset),
                PreviousLine = TextSegmenter.GetPreviousLine(fullText, caretOffset),
                CaretOffset = caretOffset,
                Selection = selection
            };
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "UIA text read failed");
            return null;
        }
    }

    private static IUIAutomationTextRange? CreateRange(IUIAutomationTextPattern pattern, TextSpan span)
    {
        try
        {
            var doc = pattern.DocumentRange;
            var range = doc.Clone();
            // Collapse to document start, then walk both endpoints to [start, start+length).
            range.MoveEndpointByRange(
                TextPatternRangeEndpoint.TextPatternRangeEndpoint_End,
                doc,
                TextPatternRangeEndpoint.TextPatternRangeEndpoint_Start);
            if (span.Start > 0)
            {
                range.MoveEndpointByUnit(
                    TextPatternRangeEndpoint.TextPatternRangeEndpoint_Start,
                    TextUnit.TextUnit_Character,
                    span.Start);
                range.MoveEndpointByUnit(
                    TextPatternRangeEndpoint.TextPatternRangeEndpoint_End,
                    TextUnit.TextUnit_Character,
                    span.Start);
            }

            if (span.Length > 0)
            {
                range.MoveEndpointByUnit(
                    TextPatternRangeEndpoint.TextPatternRangeEndpoint_End,
                    TextUnit.TextUnit_Character,
                    span.Length);
            }

            return range;
        }
        catch
        {
            return null;
        }
    }

    private static int ResolveCaretOffset(IUIAutomationTextPattern pattern, IUIAutomationTextRange document)
    {
        if (pattern is not IUIAutomationTextPattern2 pattern2)
        {
            return -1;
        }

        try
        {
            var caret = pattern2.GetCaretRange(out _);
            if (caret is null)
            {
                return -1;
            }

            return caret.CompareEndpoints(
                TextPatternRangeEndpoint.TextPatternRangeEndpoint_Start,
                document,
                TextPatternRangeEndpoint.TextPatternRangeEndpoint_Start);
        }
        catch
        {
            return -1;
        }
    }
}
