using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Core.Text;
using Interop.UIAutomationClient;

namespace Typee.Platform.Windows.Uia;

public sealed class UiaTextContextReader : ITextContextReader
{
    private readonly UiaThread _uiaThread;
    private readonly UiaElementCache _elementCache;
    private readonly ITargetContextGuard _guard;
    private readonly ILogger<UiaTextContextReader> _logger;

    public UiaTextContextReader(
        UiaThread uiaThread,
        UiaElementCache elementCache,
        ITargetContextGuard guard,
        ILogger<UiaTextContextReader> logger)
    {
        _uiaThread = uiaThread;
        _elementCache = elementCache;
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
            var pattern = GetTextPattern(automation);
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
            var pattern = GetTextPattern(automation);
            if (pattern is null)
            {
                return false;
            }

            try
            {
                var doc = pattern.DocumentRange;
                var range = doc.Clone();
                range.MoveEndpointByRange(TextPatternRangeEndpoint.TextPatternRangeEndpoint_Start, doc, TextPatternRangeEndpoint.TextPatternRangeEndpoint_Start);
                range.MoveEndpointByUnit(TextPatternRangeEndpoint.TextPatternRangeEndpoint_End, TextUnit.TextUnit_Character, span.Start);
                range.MoveEndpointByUnit(TextPatternRangeEndpoint.TextPatternRangeEndpoint_End, TextUnit.TextUnit_Character, span.Length);
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

    private TextContext? ReadOnThread(CUIAutomation8 automation, int maxContextChars)
    {
        var pattern = GetTextPattern(automation);
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

    private IUIAutomationTextPattern? GetTextPattern(CUIAutomation8 automation)
    {
        var element = _elementCache.GetFocusedElement(automation);
        if (element is null)
        {
            return null;
        }

        return UiaPatternHelper.TryGetTextPattern(element);
    }

    private static IUIAutomationTextRange? CreateRange(IUIAutomationTextPattern pattern, TextSpan span)
    {
        try
        {
            var range = pattern.DocumentRange.Clone();
            range.MoveEndpointByUnit(TextPatternRangeEndpoint.TextPatternRangeEndpoint_Start, TextUnit.TextUnit_Character, span.Start);
            range.MoveEndpointByUnit(TextPatternRangeEndpoint.TextPatternRangeEndpoint_End, TextUnit.TextUnit_Character, span.Start);
            if (span.Length > 0)
            {
                range.MoveEndpointByUnit(TextPatternRangeEndpoint.TextPatternRangeEndpoint_End, TextUnit.TextUnit_Character, span.Length);
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
