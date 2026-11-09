using Typee.Core.Models;

namespace Typee.Core.Abstractions;

/// <summary>Offline grammar checker (Harper LSP with mini-checker fallback).</summary>
public interface IGrammarChecker : IAsyncDisposable
{
    /// <summary>Short status for Diagnostics / tray (never includes user text).</summary>
    string Status { get; }

    /// <summary>True when Harper LSP is connected; false when using fallback or disabled.</summary>
    bool IsHarperActive { get; }

    /// <summary>True after restart budget exhausted or explicit disable.</summary>
    bool IsDisabled { get; }

    event EventHandler? StatusChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Ensure the child process is alive (e.g. after SystemResumed).</summary>
    Task EnsureAliveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Check a single paragraph. <paramref name="caretOffsetInParagraph"/> is used
    /// to prefer the nearest issue before the caret and to ignore the word being typed.
    /// Never logs the paragraph text.
    /// </summary>
    Task<IReadOnlyList<GrammarIssue>> CheckAsync(
        string paragraph,
        int caretOffsetInParagraph,
        CancellationToken cancellationToken = default);

    Task AddToDictionaryAsync(string word, CancellationToken cancellationToken = default);
}
