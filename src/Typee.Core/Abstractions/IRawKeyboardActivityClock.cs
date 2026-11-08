namespace Typee.Core.Abstractions;

/// <summary>
/// Tracks only the tick of the last raw keyboard event (no key codes, no text).
/// </summary>
public interface IRawKeyboardActivityClock
{
    int LastRawKeyboardTick { get; }

    /// <summary>Records that a keyboard event occurred now. Must not accept or store which key.</summary>
    void NoteKeyboardActivity();
}
