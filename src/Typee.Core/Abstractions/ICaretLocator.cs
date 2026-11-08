using Typee.Core.Models;

namespace Typee.Core.Abstractions;

public interface ICaretLocator
{
    /// <summary>Returns caret position in physical screen pixels, or null when skipped/unavailable.</summary>
    CaretLocation? TryLocate();
}
