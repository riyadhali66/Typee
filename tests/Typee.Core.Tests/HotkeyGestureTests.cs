using Typee.Core.Input;

namespace Typee.Core.Tests;

public sealed class HotkeyGestureTests
{
    [Theory]
    [InlineData("Ctrl+Shift+Space", KeyModifiers.Ctrl | KeyModifiers.Shift, 0x20, "Ctrl+Shift+Space")]
    [InlineData("ctrl+alt+m", KeyModifiers.Ctrl | KeyModifiers.Alt, 'M', "Ctrl+Alt+M")]
    [InlineData("Ctrl + Alt + Enter", KeyModifiers.Ctrl | KeyModifiers.Alt, 0x0D, "Ctrl+Alt+Enter")]
    [InlineData("Tab", KeyModifiers.None, 0x09, "Tab")]
    [InlineData("escape", KeyModifiers.None, 0x1B, "Escape")]
    [InlineData("Win+L", KeyModifiers.Win, 'L', "Win+L")]
    [InlineData("Shift+F5", KeyModifiers.Shift, 0x74, "Shift+F5")]
    [InlineData("Alt+X", KeyModifiers.Alt, 'X', "Alt+X")]
    [InlineData("Ctrl+.", KeyModifiers.Ctrl, 0xBE, "Ctrl+.")]
    public void TryParse_ParsesAndNormalizes(string text, KeyModifiers modifiers, int vk, string normalized)
    {
        var ok = HotkeyGesture.TryParse(text, out var gesture, out var error);

        Assert.True(ok, error);
        Assert.Equal(modifiers, gesture.Modifiers);
        Assert.Equal(vk, gesture.VirtualKey);
        Assert.Equal(normalized, gesture.NormalizedText);
    }

    [Theory]
    [InlineData("", "empty")]
    [InlineData("   ", "empty")]
    [InlineData("Ctrl+", "empty segment")]
    [InlineData("+A", "empty segment")]
    [InlineData("Ctrl", "no key")]
    [InlineData("Ctrl+Shift", "no key")]
    [InlineData("Ctrl+Banana", "not a recognized key")]
    [InlineData("A+B", "more than one key")]
    [InlineData("Alt+Space", "system menu")]
    [InlineData("alt+space", "system menu")]
    public void TryParse_RejectsInvalidInput(string text, string expectedErrorPart)
    {
        var ok = HotkeyGesture.TryParse(text, out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Contains(expectedErrorPart, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ThrowsFormatError_OnInvalidInput()
    {
        Assert.Throws<FormatException>(() => HotkeyGesture.Parse("Ctrl+Banana"));
    }

    [Fact]
    public void HasModifiers_ReflectsModifierPresence()
    {
        Assert.True(HotkeyGesture.Parse("Ctrl+Shift+Space").HasModifiers);
        Assert.False(HotkeyGesture.Parse("Tab").HasModifiers);
    }

    [Fact]
    public void KeyNames_RoundTrip()
    {
        foreach (var text in new[] { "Ctrl+Shift+Space", "Ctrl+Alt+Enter", "Ctrl+Alt+L", "Ctrl+Alt+X", "Ctrl+Alt+M", "Tab", "Escape" })
        {
            var gesture = HotkeyGesture.Parse(text);
            Assert.Equal(text, gesture.NormalizedText);
        }
    }

    [Theory]
    [InlineData(0xA0)] // VK_LSHIFT
    [InlineData(0xA1)] // VK_RSHIFT
    [InlineData(0xA2)] // VK_LCONTROL
    [InlineData(0xA3)] // VK_RCONTROL
    [InlineData(0xA4)] // VK_LMENU
    [InlineData(0xA5)] // VK_RMENU
    public void IsModifierKey_IncludesSideSpecificCodesTheHookReports(int vk)
    {
        Assert.True(VirtualKeys.IsModifierKey(vk));
    }
}
