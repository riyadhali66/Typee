namespace Typee.Core.Insertion;

/// <summary>
/// Opaque target identity for pre-insert verification. Never contains document text.
/// <see cref="RuntimeIdKey"/> is a stable join of UIA RuntimeId integers (empty when unavailable).
/// </summary>
public readonly record struct FocusIdentity(nint WindowHandle, string RuntimeIdKey)
{
    public bool IsEmpty => WindowHandle == 0;

    public static FocusIdentity Empty => default;

    public static string FormatRuntimeId(IReadOnlyList<int>? runtimeId)
    {
        if (runtimeId is null || runtimeId.Count == 0)
        {
            return string.Empty;
        }

        return string.Join('.', runtimeId);
    }

    public bool Matches(FocusIdentity other)
    {
        if (WindowHandle == 0 || other.WindowHandle == 0 || WindowHandle != other.WindowHandle)
        {
            return false;
        }

        // Both missing RuntimeId → window-only (weak). Prefer requiring RuntimeId for replace.
        if (RuntimeIdKey.Length == 0 && other.RuntimeIdKey.Length == 0)
        {
            return true;
        }

        // One side missing RuntimeId → do not treat as a match (avoids wrong-control inserts).
        if (RuntimeIdKey.Length == 0 || other.RuntimeIdKey.Length == 0)
        {
            return false;
        }

        return string.Equals(RuntimeIdKey, other.RuntimeIdKey, StringComparison.Ordinal);
    }
}
