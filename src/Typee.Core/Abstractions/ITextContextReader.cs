using Typee.Core.Models;

namespace Typee.Core.Abstractions;

public interface ITextContextReader
{
    /// <summary>Reads text context via UIA TextPattern. Returns null when TextPattern is unavailable.</summary>
    TextContext? TryRead(int maxContextChars);

    ScreenRect? TryGetCharacterRangeScreenRect(TextSpan span);

    bool TrySelect(TextSpan span);

    /// <summary>
    /// Reads the exact document span text for pre-insert verification.
    /// Returns null when TextPattern is unavailable or the range cannot be read. Never log the result.
    /// </summary>
    string? TryReadSpanText(TextSpan span);

    /// <summary>Moves the caret to a document character offset (collapsed selection). Returns false on failure.</summary>
    bool TrySetCaretOffset(int caretOffset);
}
