using Typee.Core.Models;

namespace Typee.Core.Insertion;

/// <summary>
/// How to apply an accepted suggestion. May hold user text — never log it.
/// </summary>
public sealed class TextInsertPlan
{
    public required TextInsertAction Action { get; init; }

    /// <summary>Primary insert/replacement text (option 0).</summary>
    public required string InsertText { get; init; }

    /// <summary>Per-option insert texts when Alt+1/2/3 selects an alternate grammar fix.</summary>
    public IReadOnlyList<string> InsertOptions { get; init; } = Array.Empty<string>();

    /// <summary>Document span to replace when <see cref="Action"/> is <see cref="TextInsertAction.ReplaceSpan"/>.</summary>
    public TextSpan? ReplaceSpan { get; init; }

    /// <summary>Expected UTF-16 text currently at <see cref="ReplaceSpan"/> (verification).</summary>
    public string? ExpectedSpanText { get; init; }

    /// <summary>
    /// Short suffix of text-before-caret at suggestion time (append verification).
    /// Compared against a fresh read; never logged.
    /// </summary>
    public string? ExpectedContextSuffix { get; init; }

    public FocusIdentity Target { get; init; }

    public TextInsertMode PreferredMode { get; init; } = TextInsertMode.SendInput;

    public string ResolveInsertText(int optionIndex)
    {
        if (InsertOptions.Count > 0)
        {
            var index = Math.Clamp(optionIndex, 0, InsertOptions.Count - 1);
            return InsertOptions[index];
        }

        return InsertText;
    }
}
