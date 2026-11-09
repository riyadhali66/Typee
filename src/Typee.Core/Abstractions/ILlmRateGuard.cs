namespace Typee.Core.Abstractions;

/// <summary>Per-profile rate/cost guard. When paused, LLM features stop; local features keep working.</summary>
public interface ILlmRateGuard
{
    bool IsPaused(string profileName);

    /// <summary>True if a request may proceed (and is recorded). False when paused/capped.</summary>
    bool TryAcquire(string profileName);

    string? PauseReason(string profileName);

    event EventHandler<LlmRateGuardPausedEventArgs>? Paused;
}

public sealed class LlmRateGuardPausedEventArgs : EventArgs
{
    public required string ProfileName { get; init; }
    public required string Reason { get; init; }
}
