namespace Typee.Core.Models;

/// <summary>Parsed LLM deep style-check JSON. Never log Corrected/Explanation (may quote user text).</summary>
public sealed class StyleCheckResult
{
    public bool HasIssue { get; init; }
    public string Corrected { get; init; } = string.Empty;
    public string Explanation { get; init; } = string.Empty;
}
