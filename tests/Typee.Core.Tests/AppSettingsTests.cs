using Typee.Core.Settings;

namespace Typee.Core.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void CreateDefault_MatchesPhase1ExampleShape()
    {
        var settings = AppSettings.CreateDefault();

        Assert.Equal("Ctrl+Shift+Space", settings.Hotkeys.ToggleWidget);
        Assert.Equal("Ctrl+Alt+M", settings.Hotkeys.PushToTalk);
        Assert.Equal("Tab", settings.Hotkeys.AcceptSuggestion);
        Assert.Equal("Escape", settings.Hotkeys.Dismiss);

        Assert.True(settings.Suggestions.Enabled);
        Assert.Equal(500, settings.Suggestions.DebounceMs);
        Assert.Equal(12, settings.Suggestions.MinCharsBeforeSuggest);
        Assert.Equal(1500, settings.Suggestions.MaxContextChars);

        Assert.Equal("harper", settings.Grammar.Engine);
        Assert.Equal("American", settings.Grammar.Dialect);
        Assert.Equal(400, settings.Grammar.RequestTimeoutMs);

        Assert.Equal("http://localhost:11434/v1", settings.Llm.BaseUrl);
        Assert.Equal("llama3.1:8b", settings.Llm.Model);
        Assert.Equal("TYPEE_API_KEY", settings.Llm.ApiKeyEnvVar);

        Assert.Equal("base.en", settings.Speech.Model);
        Assert.Equal("SendInput", settings.Speech.InsertMode);

        Assert.Contains("KeePassXC.exe", settings.BlockedProcesses);
        Assert.Contains("mstsc.exe", settings.BlockedProcesses);
        Assert.False(settings.StartWithWindows);
    }
}
