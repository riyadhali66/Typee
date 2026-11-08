namespace Typee.Core.Input;

/// <summary>
/// A single observed keyboard event. Contains NO character data — only the virtual-key
/// code, LL-hook flags, modifier snapshot, and timing — so it is safe to pass around under
/// the "never log keystrokes" rule. Injected events are filtered before enqueue; <see cref="IsInjected"/>
/// is therefore always false on the public channel.
/// </summary>
public readonly record struct KeyboardEvent(
    int VirtualKey,
    bool IsKeyDown,
    int Flags,
    KeyModifiers Modifiers,
    uint Timestamp,
    bool IsInjected = false);
