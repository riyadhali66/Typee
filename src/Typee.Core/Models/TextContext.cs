namespace Typee.Core.Models;

/// <summary>Text read from UIA for the focused control. Must never be logged.</summary>
public sealed class TextContext
{
    public string TextBeforeCaret { get; init; } = string.Empty;
    public string CurrentSentence { get; init; } = string.Empty;
    public string CurrentParagraph { get; init; } = string.Empty;
    public string CurrentLine { get; init; } = string.Empty;
    public string? PreviousLine { get; init; }
    public int CaretOffset { get; init; }
    public TextSpan? Selection { get; init; }
}
