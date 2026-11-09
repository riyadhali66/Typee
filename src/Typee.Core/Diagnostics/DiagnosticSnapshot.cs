namespace Typee.Core.Diagnostics;

/// <summary>
/// Live diagnostics payload for the Diagnostics window. Never contains user text.
/// </summary>
public sealed class DiagnosticSnapshot
{
    public double CpuPercent { get; init; }
    public long WorkingSetBytes { get; init; }
    public bool HookInstalled { get; init; }
    public DateTimeOffset? LastHookReinstallUtc { get; init; }
    public long HookDroppedEvents { get; init; }
    public string CaretMethod { get; init; } = "—";
    public string? ForegroundProcess { get; init; }
    public string HarperStatus { get; init; } = "Not started (Phase 6)";
    public string LlmStatus { get; init; } = "Not started (Phase 7)";
    public IReadOnlyList<string> RecentErrors { get; init; } = Array.Empty<string>();
}
