using Typee.Core.Insertion;
using Typee.Core.Models;

namespace Typee.Core.Abstractions;

/// <summary>
/// Inserts or replaces text in the focused target. Never logs document or clipboard text.
/// Implementations must fail safely and never run inside the keyboard-hook callback.
/// </summary>
public interface ITextInserter
{
    /// <summary>Appends <paramref name="text"/> at the current caret using the chosen mode.</summary>
    Task<bool> InsertAsync(string text, TextInsertMode mode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects <see cref="TextReplaceRequest.Span"/> (UIA preferred), verifies the target is unchanged,
    /// inserts the replacement, and restores the caret. Aborts silently when verification fails.
    /// </summary>
    Task<bool> ReplaceSpanAsync(TextReplaceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Applies a suggestion insert plan (append or replace) after verification.</summary>
    Task<bool> ApplyPlanAsync(TextInsertPlan plan, string insertText, CancellationToken cancellationToken = default);
}
