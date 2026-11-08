using Typee.Core.Models;

namespace Typee.Core.Abstractions;

public interface ITextContextReader
{
    /// <summary>Reads text context via UIA TextPattern. Returns null when TextPattern is unavailable.</summary>
    TextContext? TryRead(int maxContextChars);

    ScreenRect? TryGetCharacterRangeScreenRect(TextSpan span);

    bool TrySelect(TextSpan span);
}
