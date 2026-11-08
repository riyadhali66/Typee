namespace Typee.Core.Settings;

public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public bool PauseInFullscreen { get; set; } = true;
    public string UiLanguage { get; set; } = "auto";

    public HotkeySettings Hotkeys { get; set; } = new();
    public SuggestionSettings Suggestions { get; set; } = new();
    public GrammarSettings Grammar { get; set; } = new();
    public LlmSettings Llm { get; set; } = new();
    public ButtonSettings Button { get; set; } = new();
    public SmartListsSettings SmartLists { get; set; } = new();
    public PromptAssistSettings PromptAssist { get; set; } = new();
    public SnippetsSettings Snippets { get; set; } = new();
    public SpeechSettings Speech { get; set; } = new();
    public List<string> TranslateTargets { get; set; } = ["English", "Arabic"];
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

    public static List<string> DefaultSmartListDisabledProcesses() =>
    [
        "WINWORD.EXE",
        "ONENOTE.EXE",
        "OUTLOOK.EXE",
        "Notion.exe"
    ];

    public static List<PromptAssistTarget> DefaultPromptAssistTargets() =>
    [
        new()
        {
            Id = "claude-web",
            Label = "Claude",
            Method = "clipboard-open",
            Url = "https://claude.ai/new",
            AutoSubmit = false
        },
        new()
        {
            Id = "chatgpt-web",
            Label = "ChatGPT",
            Method = "clipboard-open",
            Url = "https://chatgpt.com/",
            AutoSubmit = false
        },
        new()
        {
            Id = "gemini-web",
            Label = "Gemini",
            Method = "clipboard-open",
            Url = "https://gemini.google.com/app",
            AutoSubmit = false
        },
        new()
        {
            Id = "cursor",
            Label = "Cursor",
            Method = "clipboard-focus",
            Process = "Cursor.exe",
            OpenChatKeys = "Ctrl+L",
            AutoSubmit = false
        }
    ];
}

public sealed class HotkeySettings
{
    public string CommandBar { get; set; } = "Ctrl+Shift+Space";
    public string SendToLastAi { get; set; } = "Ctrl+Alt+Enter";
    public string ListStylePicker { get; set; } = "Ctrl+Alt+L";
    public string ToggleChecklist { get; set; } = "Ctrl+Alt+X";
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

public sealed class ButtonSettings
{
    public bool Enabled { get; set; } = true;
    public bool HideWhileTyping { get; set; } = true;
}

public sealed class SmartListsSettings
{
    public bool Enabled { get; set; } = true;
    public bool AutoContinue { get; set; } = true;
    public bool TabIndent { get; set; } = true;
    public string DefaultNumberStyle { get; set; } = "1.";
    public string DefaultBullet { get; set; } = "•";
    public List<string> DisabledProcesses { get; set; } = AppSettings.DefaultSmartListDisabledProcesses();
}

public sealed class PromptAssistSettings
{
    public bool DetectPrompts { get; set; } = true;
    public double DetectionThreshold { get; set; } = 0.6;
    public string LastTarget { get; set; } = "claude-web";
    public List<PromptAssistTarget> Targets { get; set; } = AppSettings.DefaultPromptAssistTargets();
}

public sealed class PromptAssistTarget
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Method { get; set; } = "clipboard-open";
    public string? Url { get; set; }
    public string? Process { get; set; }
    public string? OpenChatKeys { get; set; }
    public bool AutoSubmit { get; set; }
}

public sealed class SnippetsSettings
{
    public bool Enabled { get; set; } = true;
    public string TriggerPrefix { get; set; } = ";";
}

public sealed class SpeechSettings
{
    public string Model { get; set; } = "base.en";
    public string Language { get; set; } = "en";
    public string InsertMode { get; set; } = "SendInput";
}
