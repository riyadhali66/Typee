namespace Typee.Core.Models;

/// <summary>Raised when the user accepts a suggestion (Tab / Alt+n). May hold user text — never log it.</summary>
public sealed class SuggestionAcceptedEventArgs : EventArgs
{
    public required Suggestion Suggestion { get; init; }

    /// <summary>Resolved insert/replacement text for the chosen option.</summary>
    public required string InsertText { get; init; }

    public int OptionIndex { get; init; }
}
