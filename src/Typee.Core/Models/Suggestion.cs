using Typee.Core.Insertion;

namespace Typee.Core.Models;

/// <summary>
/// A floating overlay suggestion. Contains replacement text for display/insert — never logged.
/// </summary>
public sealed class Suggestion
{
    public required string HeaderKey { get; init; }
    public required string Body { get; init; }

    /// <summary>Unchanged prefix shown before the highlighted span (may be empty).</summary>
    public string Prefix { get; init; } = string.Empty;

    /// <summary>Changed / new text highlighted in the body.</summary>
    public string Highlight { get; init; } = string.Empty;

    /// <summary>Unchanged suffix after the highlight (may be empty).</summary>
    public string Suffix { get; init; } = string.Empty;

    /// <summary>Up to three replacement options (grammar). Empty for single-body completions.</summary>
    public IReadOnlyList<string> Options { get; init; } = Array.Empty<string>();

    public SuggestionKind Kind { get; init; }
    public SuggestionLevel Level { get; init; }

    /// <summary>Primary accept text (first option, or body when no options).</summary>
    public string PrimaryText =>
        Options.Count > 0 ? Options[0] : string.Concat(Prefix, Highlight, Suffix);

    /// <summary>Caret anchor in physical pixels when shown; used for move-away hide.</summary>
    public ScreenRect? AnchorCaret { get; init; }

    public MonitorInfo? Monitor { get; init; }

    /// <summary>How Tab/Alt+n should insert into the host document (Phase 8). Never log.</summary>
    public TextInsertPlan? Insert { get; init; }
}
