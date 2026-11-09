using Typee.Core.Insertion;

namespace Typee.Core.Abstractions;

/// <summary>
/// Captures and restores the user's typing target (window, focused element, selection/caret).
/// Used by text insertion and later by the Command Bar (8C) / Send-to-AI (8D).
/// </summary>
public interface IFocusSnapshotService
{
    /// <summary>Snapshots the current foreground window, focused element identity, and selection/caret.</summary>
    FocusSnapshot? Capture();

    /// <summary>Restores focus to the snapshot target and attempts to restore selection/caret.</summary>
    bool TryRestore(FocusSnapshot snapshot);

    /// <summary>Captures only identity fields used for pre-insert verification (lighter than a full snapshot).</summary>
    FocusIdentity? CaptureIdentity();
}
