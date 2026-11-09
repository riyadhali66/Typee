namespace Typee.Core.Models;

public enum GrammarIssueKind
{
    Grammar = 0,
    Spelling = 1,
    Style = 2,
    Other = 3
}

/// <summary>
/// A grammar/spelling finding over a checked paragraph.
/// Offsets are UTF-16 code units (.NET string indices) within that paragraph.
/// Never log <see cref="Message"/> or <see cref="Replacements"/> (may quote user text).
/// </summary>
public sealed class GrammarIssue
{
    public int Start { get; init; }
    public int Length { get; init; }
    public string Message { get; init; } = string.Empty;
    public string RuleName { get; init; } = string.Empty;
    public IReadOnlyList<string> Replacements { get; init; } = [];
    public GrammarIssueKind Kind { get; init; } = GrammarIssueKind.Grammar;
    public bool CanAddToDictionary { get; init; }
}
