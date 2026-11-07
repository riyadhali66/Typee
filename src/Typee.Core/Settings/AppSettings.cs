namespace Typee.Core.Settings;

public sealed class AppSettings
{
    public HotkeySettings Hotkeys { get; set; } = new();
    public SuggestionSettings Suggestions { get; set; } = new();
    public GrammarSettings Grammar { get; set; } = new();
    public LlmSettings Llm { get; set; } = new();
    public SpeechSettings Speech { get; set; } = new();
    public List<string> BlockedProcesses { get; set; } = DefaultBlockedProcesses();
    public bool StartWithWindows { get; set; }

    public static AppSettings CreateDefault() => new();

    public static List<string> DefaultBlockedProcesses() =>
    [
        "KeePassXC.exe",
        "1Password.exe",
        "Bitwarden.exe",
        "mstsc.exe"
    ];
}

public sealed class HotkeySettings
{
    public string ToggleWidget { get; set; } = "Ctrl+Shift+Space";
    public string PushToTalk { get; set; } = "Ctrl+Alt+M";
    public string AcceptSuggestion { get; set; } = "Tab";
    public string Dismiss { get; set; } = "Escape";
}

public sealed class SuggestionSettings
{
    public bool Enabled { get; set; } = true;
    public int DebounceMs { get; set; } = 500;
    public int MinCharsBeforeSuggest { get; set; } = 12;
    public int MaxContextChars { get; set; } = 1500;
    public bool Level1GrammarEnabled { get; set; } = true;
    public bool Level2CompletionEnabled { get; set; } = true;
}

public sealed class GrammarSettings
{
    public string Engine { get; set; } = "harper";
    public string Dialect { get; set; } = "American";
    public bool LlmDeepCheck { get; set; }
    public List<string> DisabledRules { get; set; } = [];
    public int RequestTimeoutMs { get; set; } = 400;
}

public sealed class LlmSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434/v1";
    public string Model { get; set; } = "llama3.1:8b";
    public string ApiKeyEnvVar { get; set; } = "TYPEE_API_KEY";
    public int TimeoutMs { get; set; } = 8000;
    public int MaxTokens { get; set; } = 60;
    public double Temperature { get; set; } = 0.3;
}

public sealed class SpeechSettings
{
    public string Model { get; set; } = "base.en";
    public string Language { get; set; } = "en";
    public string InsertMode { get; set; } = "SendInput";
}
