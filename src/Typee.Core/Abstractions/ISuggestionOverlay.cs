using Typee.Core.Models;

namespace Typee.Core.Abstractions;

/// <summary>
/// Contract for the floating suggestion card. Implementations must never steal focus
/// (<c>WS_EX_NOACTIVATE</c>) and must drive hook swallow flags via <see cref="IsVisible"/>.
/// </summary>
public interface ISuggestionOverlay
{
    bool IsVisible { get; }

    void Show(Suggestion suggestion);

    void Hide();

    event EventHandler? VisibilityChanged;
}
