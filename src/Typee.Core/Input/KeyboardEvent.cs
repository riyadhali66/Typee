namespace Typee.Core.Input;

/// <summary>
/// A single observed keyboard event. Contains NO character data — only the virtual-key
/// code and timing — so it is safe to pass around under the "never log keystrokes" rule.
/// </summary>
public readonly record struct KeyboardEvent(int VirtualKey, bool IsKeyDown, uint Timestamp);
