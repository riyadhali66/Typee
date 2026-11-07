using System.Text.Json;
using Typee.Core.Settings;

namespace Typee.Core.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void CreateDefault_MatchesPhase1ExampleShape()
    {
        var settings = AppSettings.CreateDefault();

        Assert.Equal("Ctrl+Shift+Space", settings.Hotkeys.CommandBar);
        Assert.Equal("Ctrl+Alt+Enter", settings.Hotkeys.SendToLastAi);
        Assert.Equal("Ctrl+Alt+L", settings.Hotkeys.ListStylePicker);
        Assert.Equal("Ctrl+Alt+X", settings.Hotkeys.ToggleChecklist);
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

        Assert.True(settings.Button.Enabled);
        Assert.True(settings.Button.HideWhileTyping);

        Assert.True(settings.SmartLists.Enabled);
        Assert.Equal("1.", settings.SmartLists.DefaultNumberStyle);
        Assert.Equal("•", settings.SmartLists.DefaultBullet);
        Assert.Contains("WINWORD.EXE", settings.SmartLists.DisabledProcesses);

        Assert.True(settings.PromptAssist.DetectPrompts);
        Assert.Equal(0.6, settings.PromptAssist.DetectionThreshold, precision: 2);
        Assert.Equal("claude-web", settings.PromptAssist.LastTarget);
        Assert.Contains(settings.PromptAssist.Targets, t => t.Id == "cursor" && t.Process == "Cursor.exe" && t.OpenChatKeys == "Ctrl+L");
        Assert.DoesNotContain(settings.PromptAssist.Targets, t => t.AutoSubmit);

        Assert.True(settings.Snippets.Enabled);
        Assert.Equal(";", settings.Snippets.TriggerPrefix);

        Assert.Equal(["English", "Arabic"], settings.TranslateTargets);

        Assert.Equal("base.en", settings.Speech.Model);
        Assert.Equal("SendInput", settings.Speech.InsertMode);

        Assert.Contains("KeePassXC.exe", settings.BlockedProcesses);
        Assert.Contains("mstsc.exe", settings.BlockedProcesses);
        Assert.False(settings.StartWithWindows);
    }

    [Fact]
    public void SettingsExampleJson_ParsesIntoAppSettings_WithDefaultsIntact()
    {
        // settings.example.json is copied to the test output directory by the csproj.
        var path = Path.Combine(AppContext.BaseDirectory, "settings.example.json");
        Assert.True(File.Exists(path), $"settings.example.json was not copied next to the test assembly ({path})");

        using var stream = File.OpenRead(path);
        var settings = JsonSerializer.Deserialize<AppSettings>(stream, JsonOptions)
            ?? throw new InvalidOperationException("settings.example.json deserialized to null");

        Assert.Equal("Ctrl+Shift+Space", settings.Hotkeys.CommandBar);
        Assert.Equal("Ctrl+Alt+M", settings.Hotkeys.PushToTalk);
        Assert.Equal(4, settings.PromptAssist.Targets.Count);
        Assert.Equal(["English", "Arabic"], settings.TranslateTargets);
        Assert.Equal(";", settings.Snippets.TriggerPrefix);
        Assert.Contains("Notion.exe", settings.SmartLists.DisabledProcesses);
        Assert.Contains("KeePassXC.exe", settings.BlockedProcesses);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
