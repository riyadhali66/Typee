using Typee.Core.Models;
using Typee.Core.Settings;

namespace Typee.Core.Suggestions;

/// <summary>
/// Shared bag for one suggestion pass. May hold user text — never log it.
/// </summary>
public sealed class SuggestionPipelineContext
{
    public required TextContext Text { get; init; }
    public required AppSettings Settings { get; init; }
    public required CaretLocation? Caret { get; init; }

    /// <summary>True when consent accepted, rate guard allows, and endpoint policy permits LLM.</summary>
    public bool LlmAllowed { get; init; }

    /// <summary>Set by grammar provider so completion can bail when L1 already found issues.</summary>
    public bool GrammarIssueFound { get; set; }

    public IReadOnlyList<GrammarIssue> GrammarIssues { get; set; } = [];
}
