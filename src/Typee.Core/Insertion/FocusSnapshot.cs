using Typee.Core.Models;

namespace Typee.Core.Insertion;

/// <summary>
/// Captured typing target for later restore (Command Bar / Send-to-AI). Never log selection text.
/// </summary>
public sealed class FocusSnapshot
{
    public required FocusIdentity Identity { get; init; }

    /// <summary>Document caret offset at capture time (−1 when unknown).</summary>
    public int CaretOffset { get; init; } = -1;

    /// <summary>Non-empty selection when present.</summary>
    public TextSpan? Selection { get; init; }

    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
}
