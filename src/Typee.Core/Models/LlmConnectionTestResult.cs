namespace Typee.Core.Models;

public sealed class LlmConnectionTestResult
{
    public required bool Success { get; init; }
    public required string Message { get; init; }
    public TimeSpan Latency { get; init; }
}
