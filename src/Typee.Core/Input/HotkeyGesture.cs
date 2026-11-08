using System.Diagnostics.CodeAnalysis;

namespace Typee.Core.Input;

/// <summary>
/// Modifier flags for a hotkey gesture. Values match RegisterHotKey's MOD_* constants
/// so Platform code can cast directly.
/// </summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Win = 8
}

/// <summary>
/// A parsed hotkey gesture like "Ctrl+Shift+Space". Pure logic — no Win32 calls.
/// </summary>
public readonly record struct HotkeyGesture(KeyModifiers Modifiers, int VirtualKey, string NormalizedText)
{
    /// <summary>
    /// Bare keys (no modifiers) cannot be registered with RegisterHotKey without swallowing
    /// every press of that key system-wide, so they are only ever observed passively via the
    /// low-level hook (e.g. Tab while a suggestion is visible).
    /// </summary>
    public bool HasModifiers => Modifiers != KeyModifiers.None;

    public static bool TryParse(string text, out HotkeyGesture gesture, [NotNullWhen(false)] out string? error)
    {
        gesture = default;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Hotkey is empty.";
            return false;
        }

        var modifiers = KeyModifiers.None;
        var virtualKey = -1;
        var keyToken = string.Empty;

        foreach (var rawToken in text.Split('+'))
        {
            var token = rawToken.Trim();
            if (token.Length == 0)
            {
                error = $"'{text}' contains an empty segment.";
                return false;
            }

            switch (token.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= KeyModifiers.Ctrl;
                    continue;
                case "SHIFT":
                    modifiers |= KeyModifiers.Shift;
                    continue;
                case "ALT":
                    modifiers |= KeyModifiers.Alt;
                    continue;
                case "WIN":
                case "WINDOWS":
                    modifiers |= KeyModifiers.Win;
                    continue;
            }

            if (virtualKey >= 0)
            {
                error = $"'{text}' contains more than one key.";
                return false;
            }

            if (!VirtualKeyNames.TryGetVirtualKey(token, out virtualKey))
            {
                error = $"'{token}' is not a recognized key name.";
                return false;
            }

            keyToken = NormalizeKeyToken(token);
        }

        if (virtualKey < 0)
        {
            error = $"'{text}' has no key (modifiers only).";
            return false;
        }

        // Spec §5: never use Alt+Space (Windows system menu).
        if (modifiers == KeyModifiers.Alt && virtualKey == VirtualKeys.Space)
        {
            error = "Alt+Space is reserved for the Windows system menu.";
            return false;
        }

        gesture = new HotkeyGesture(modifiers, virtualKey, ComposeNormalizedText(modifiers, keyToken));
        return true;
    }

    public static HotkeyGesture Parse(string text) =>
        TryParse(text, out var gesture, out var error)
            ? gesture
            : throw new FormatException($"{error} (input: '{text}')");

    private static string NormalizeKeyToken(string token) => token.Length == 1
        ? token.ToUpperInvariant()
        : token.Length is 2 && token[0] == 'F' && char.IsDigit(token[1])
            ? "F" + token[1]
            : char.ToUpperInvariant(token[0]) + token.ToLowerInvariant()[1..];

    private static string ComposeNormalizedText(KeyModifiers modifiers, string keyToken)
    {
        var parts = new List<string>(5);
        if ((modifiers & KeyModifiers.Ctrl) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & KeyModifiers.Shift) != 0)
        {
            parts.Add("Shift");
        }

        if ((modifiers & KeyModifiers.Alt) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers & KeyModifiers.Win) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(keyToken);
        return string.Join("+", parts);
    }
}

public static class VirtualKeys
{
    public const int Backspace = 0x08;
    public const int Tab = 0x09;
    public const int Enter = 0x0D;
    public const int Shift = 0x10;
    public const int Control = 0x11;
    public const int Alt = 0x12;
    public const int Escape = 0x1B;
    public const int Space = 0x20;
    public const int PageUp = 0x21;
    public const int PageDown = 0x22;
    public const int End = 0x23;
    public const int Home = 0x24;
    public const int Left = 0x25;
    public const int Up = 0x26;
    public const int Right = 0x27;
    public const int Down = 0x28;
    public const int Insert = 0x2D;
    public const int Delete = 0x2E;
    public const int LWin = 0x5B;
    public const int RWin = 0x5C;
    public const int LShift = 0xA0;
    public const int RShift = 0xA1;
    public const int LControl = 0xA2;
    public const int RControl = 0xA3;
    public const int LAlt = 0xA4;
    public const int RAlt = 0xA5;

    public static bool IsModifierKey(int vk) =>
        vk is Shift or Control or Alt or LWin or RWin
            or LShift or RShift or LControl or RControl or LAlt or RAlt;
}

/// <summary>Key-name ↔ virtual-key table. Pure data, unit-testable.</summary>
public static class VirtualKeyNames
{
    private static readonly Dictionary<string, int> Map = BuildMap();

    public static bool TryGetVirtualKey(string token, out int virtualKey)
    {
        if (token.Length == 1)
        {
            var c = char.ToUpperInvariant(token[0]);
            if (c is >= 'A' and <= 'Z')
            {
                virtualKey = c;
                return true;
            }

            if (c is >= '0' and <= '9')
            {
                virtualKey = c;
                return true;
            }

            if (Punctuation.TryGetValue(c, out virtualKey))
            {
                return true;
            }
        }

        return Map.TryGetValue(token.ToUpperInvariant(), out virtualKey);
    }

    public static bool TryGetKeyName(int virtualKey, [NotNullWhen(true)] out string? name)
    {
        foreach (var (keyName, vk) in Map)
        {
            if (vk == virtualKey)
            {
                name = keyName;
                return true;
            }
        }

        if (virtualKey is >= 'A' and <= 'Z' or >= '0' and <= '9')
        {
            name = ((char)virtualKey).ToString();
            return true;
        }

        foreach (var (c, vk) in Punctuation)
        {
            if (vk == virtualKey)
            {
                name = c.ToString();
                return true;
            }
        }

        name = null;
        return false;
    }

    private static Dictionary<string, int> BuildMap()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["SPACE"] = VirtualKeys.Space,
            ["SPACEBAR"] = VirtualKeys.Space,
            ["ENTER"] = VirtualKeys.Enter,
            ["RETURN"] = VirtualKeys.Enter,
            ["ESC"] = VirtualKeys.Escape,
            ["ESCAPE"] = VirtualKeys.Escape,
            ["TAB"] = VirtualKeys.Tab,
            ["BACKSPACE"] = VirtualKeys.Backspace,
            ["BACK"] = VirtualKeys.Backspace,
            ["DELETE"] = VirtualKeys.Delete,
            ["DEL"] = VirtualKeys.Delete,
            ["INSERT"] = VirtualKeys.Insert,
            ["INS"] = VirtualKeys.Insert,
            ["HOME"] = VirtualKeys.Home,
            ["END"] = VirtualKeys.End,
            ["PAGEUP"] = VirtualKeys.PageUp,
            ["PGUP"] = VirtualKeys.PageUp,
            ["PAGEDOWN"] = VirtualKeys.PageDown,
            ["PGDN"] = VirtualKeys.PageDown,
            ["LEFT"] = VirtualKeys.Left,
            ["UP"] = VirtualKeys.Up,
            ["RIGHT"] = VirtualKeys.Right,
            ["DOWN"] = VirtualKeys.Down,
            ["PRINTSCREEN"] = 0x2C,
            ["PRTSC"] = 0x2C,
            ["LWIN"] = VirtualKeys.LWin,
            ["RWIN"] = VirtualKeys.RWin,
            ["LEFTWINDOWS"] = VirtualKeys.LWin,
            ["RIGHTWINDOWS"] = VirtualKeys.RWin
        };

        for (var digit = 0; digit <= 9; digit++)
        {
            map[$"D{digit}"] = '0' + digit;
            map[$"NUM{digit}"] = 0x60 + digit;
            map[$"NUMPAD{digit}"] = 0x60 + digit;
        }

        for (var f = 1; f <= 24; f++)
        {
            map[$"F{f}"] = 0x6F + f;
        }

        return map;
    }

    private static readonly IReadOnlyDictionary<char, int> Punctuation = new Dictionary<char, int>
    {
        [';'] = 0xBA,
        ['='] = 0xBB,
        [','] = 0xBC,
        ['-'] = 0xBD,
        ['.'] = 0xBE,
        ['/'] = 0xBF,
        ['`'] = 0xC0,
        ['['] = 0xDB,
        ['\\'] = 0xDC,
        [']'] = 0xDD,
        ['\''] = 0xDE
    };
}
