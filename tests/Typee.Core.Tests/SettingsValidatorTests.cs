using Typee.Core.Settings;

namespace Typee.Core.Tests;

public sealed class SettingsValidatorTests
{
    [Fact]
    public void ValidateAndRepair_InvalidHotkeyAndUrl_ResetsFields()
    {
        var settings = AppSettings.CreateDefault();
        settings.Hotkeys.CommandBar = "NotARealHotkey+++";
        settings.Llm.BaseUrl = "not-a-uri";
        settings.Suggestions.DebounceMs = 1;

        var reset = SettingsValidator.ValidateAndRepair(settings);

        Assert.Contains("hotkeys.commandBar", reset);
        Assert.Contains("llm.baseUrl", reset);
        Assert.Contains("suggestions.debounceMs", reset);
        Assert.Equal("Ctrl+Shift+Space", settings.Hotkeys.CommandBar);
        Assert.Equal("http://localhost:11434/v1", settings.Llm.BaseUrl);
        Assert.Equal(500, settings.Suggestions.DebounceMs);
    }
}
