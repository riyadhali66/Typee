using Typee.Core.Models;

namespace Typee.Core.Abstractions;

/// <summary>OpenAI-compatible chat client. Never logs prompts, completions, or API keys.</summary>
public interface ILlmClient
{
    /// <summary>Short status for Diagnostics / tray (never includes user text or keys).</summary>
    string Status { get; }

    bool IsOnline { get; }

    event EventHandler? StatusChanged;

    Task<string> CompleteAsync(LlmCompletionRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<string> StreamCompleteAsync(
        LlmCompletionRequest request,
        CancellationToken cancellationToken = default);

    Task<LlmConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
}
