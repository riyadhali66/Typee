namespace Typee.Core.Suggestions;

/// <summary>
/// Pure helper: when the user types characters that match the start of the active
/// suggestion, trim the matched prefix instead of dismissing. Never logs text.
/// </summary>
public static class SuggestionTrim
{
    public static bool TryTrimPrefix(string remaining, char typed, out string trimmed)
    {
        if (remaining.Length == 0)
        {
            trimmed = remaining;
            return false;
        }

        if (char.ToLowerInvariant(remaining[0]) == char.ToLowerInvariant(typed))
        {
            trimmed = remaining[1..];
            return true;
        }

        trimmed = remaining;
        return false;
    }

    /// <summary>
    /// Maps a virtual-key + Shift snapshot to a single printable character when possible.
    /// Returns null for non-printable keys (navigation, modifiers, etc.).
    /// </summary>
    public static char? TryMapPrintable(int virtualKey, bool shift)
    {
        // Letters A–Z (VK 0x41–0x5A)
        if (virtualKey is >= 0x41 and <= 0x5A)
        {
            var c = (char)virtualKey;
            return shift ? c : char.ToLowerInvariant(c);
        }

        // Digits 0–9 top row
        if (virtualKey is >= 0x30 and <= 0x39 && !shift)
        {
            return (char)virtualKey;
        }

        return virtualKey switch
        {
            0x20 => ' ', // Space
            0xBE when !shift => '.',
            0xBC when !shift => ',',
            0xBF when !shift => '/',
            0xBA when !shift => ';',
            0xDE when !shift => '\'',
            0xBD when !shift => '-',
            0xBB when shift => '+',
            _ => null
        };
    }
}
