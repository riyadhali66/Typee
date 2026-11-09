using Typee.Core.Models;

namespace Typee.Core.Insertion;

/// <summary>Replace a verified document span. May hold user text — never log it.</summary>
public sealed class TextReplaceRequest
{
    public required TextSpan Span { get; init; }
    public required string ExpectedOriginal { get; init; }
    public required string Replacement { get; init; }
    public FocusIdentity Target { get; init; }
    public TextInsertMode Mode { get; init; } = TextInsertMode.SendInput;

    /// <summary>Caret offset to restore after replace (−1 = end of replacement).</summary>
    public int RestoreCaretOffset { get; init; } = -1;
}
