using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Settings;

namespace Typee.App.ViewModels;

public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly ISettingsStore _settingsStore;
    private readonly ILogger _logger;

    public SettingsWindowViewModel(ISettingsStore settingsStore, ILogger logger)
    {
        _settingsStore = settingsStore;
        _logger = logger;
        LoadFrom(settingsStore.Current);
    }

    [ObservableProperty]
    private string _toggleWidgetHotkey = "Ctrl+Shift+Space";

    [ObservableProperty]
    private string _pushToTalkHotkey = "Ctrl+Alt+M";

    [ObservableProperty]
    private string _acceptSuggestionHotkey = "Tab";

    [ObservableProperty]
    private string _dismissHotkey = "Escape";

    [ObservableProperty]
    private bool _suggestionsEnabled = true;

    [ObservableProperty]
    private int _debounceMs = 500;

    [ObservableProperty]
    private int _minCharsBeforeSuggest = 12;

    [ObservableProperty]
    private int _maxContextChars = 1500;

    [ObservableProperty]
    private bool _level1GrammarEnabled = true;

    [ObservableProperty]
    private bool _level2CompletionEnabled = true;

    [ObservableProperty]
    private string _grammarDialect = "American";

    [ObservableProperty]
    private bool _llmDeepCheck;

    [ObservableProperty]
    private string _disabledRulesText = string.Empty;

    [ObservableProperty]
    private int _grammarTimeoutMs = 400;

    [ObservableProperty]
    private string _llmBaseUrl = "http://localhost:11434/v1";

    [ObservableProperty]
    private string _llmModel = "llama3.1:8b";

    [ObservableProperty]
    private string _apiKeyEnvVar = "TYPEE_API_KEY";

    [ObservableProperty]
    private int _llmTimeoutMs = 8000;

    [ObservableProperty]
    private int _llmMaxTokens = 60;

    [ObservableProperty]
    private double _llmTemperature = 0.3;

    [ObservableProperty]
    private string _speechModel = "base.en";

    [ObservableProperty]
    private string _speechLanguage = "en";

    [ObservableProperty]
    private string _speechInsertMode = "SendInput";

    [ObservableProperty]
    private string _blockedProcessesText = string.Empty;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _capturingHotkeyTarget = string.Empty;

    public ObservableCollection<string> Dialects { get; } = ["American", "British", "Australian", "Canadian"];

    public void BeginHotkeyCapture(string target)
    {
        CapturingHotkeyTarget = target;
        StatusMessage = $"Press a key combination for {target}… (Esc cancels capture)";
    }

    public bool TryApplyCapturedHotkey(string hotkeyText)
    {
        if (string.IsNullOrWhiteSpace(CapturingHotkeyTarget))
        {
            return false;
        }

        switch (CapturingHotkeyTarget)
        {
            case nameof(ToggleWidgetHotkey):
                ToggleWidgetHotkey = hotkeyText;
                break;
            case nameof(PushToTalkHotkey):
                PushToTalkHotkey = hotkeyText;
                break;
            case nameof(AcceptSuggestionHotkey):
                AcceptSuggestionHotkey = hotkeyText;
                break;
            case nameof(DismissHotkey):
                DismissHotkey = hotkeyText;
                break;
            default:
                return false;
        }

        CapturingHotkeyTarget = string.Empty;
        StatusMessage = $"Captured {hotkeyText}";
        return true;
    }

    public void CancelHotkeyCapture()
    {
        CapturingHotkeyTarget = string.Empty;
        StatusMessage = "Hotkey capture cancelled";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            var settings = BuildSettings();
            await _settingsStore.SaveAsync(settings).ConfigureAwait(true);
            StatusMessage = $"Saved {DateTime.Now:HH:mm:ss}";
            _logger.LogInformation("Settings saved from UI");
        }
        catch (Exception ex)
        {
            StatusMessage = "Save failed — see logs";
            _logger.LogError(ex, "Failed to save settings from UI");
        }
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        try
        {
            var settings = await _settingsStore.LoadAsync().ConfigureAwait(true);
            LoadFrom(settings);
            StatusMessage = "Reloaded from disk";
        }
        catch (Exception ex)
        {
            StatusMessage = "Reload failed — see logs";
            _logger.LogError(ex, "Failed to reload settings");
        }
    }

    private void LoadFrom(AppSettings settings)
    {
        ToggleWidgetHotkey = settings.Hotkeys.ToggleWidget;
        PushToTalkHotkey = settings.Hotkeys.PushToTalk;
        AcceptSuggestionHotkey = settings.Hotkeys.AcceptSuggestion;
        DismissHotkey = settings.Hotkeys.Dismiss;

        SuggestionsEnabled = settings.Suggestions.Enabled;
        DebounceMs = settings.Suggestions.DebounceMs;
        MinCharsBeforeSuggest = settings.Suggestions.MinCharsBeforeSuggest;
        MaxContextChars = settings.Suggestions.MaxContextChars;
        Level1GrammarEnabled = settings.Suggestions.Level1GrammarEnabled;
        Level2CompletionEnabled = settings.Suggestions.Level2CompletionEnabled;

        GrammarDialect = settings.Grammar.Dialect;
        LlmDeepCheck = settings.Grammar.LlmDeepCheck;
        DisabledRulesText = string.Join(", ", settings.Grammar.DisabledRules);
        GrammarTimeoutMs = settings.Grammar.RequestTimeoutMs;

        LlmBaseUrl = settings.Llm.BaseUrl;
        LlmModel = settings.Llm.Model;
        ApiKeyEnvVar = settings.Llm.ApiKeyEnvVar;
        LlmTimeoutMs = settings.Llm.TimeoutMs;
        LlmMaxTokens = settings.Llm.MaxTokens;
        LlmTemperature = settings.Llm.Temperature;

        SpeechModel = settings.Speech.Model;
        SpeechLanguage = settings.Speech.Language;
        SpeechInsertMode = settings.Speech.InsertMode;

        BlockedProcessesText = string.Join(Environment.NewLine, settings.BlockedProcesses);
        StartWithWindows = settings.StartWithWindows;
    }

    private AppSettings BuildSettings()
    {
        return new AppSettings
        {
            Hotkeys = new HotkeySettings
            {
                ToggleWidget = ToggleWidgetHotkey.Trim(),
                PushToTalk = PushToTalkHotkey.Trim(),
                AcceptSuggestion = AcceptSuggestionHotkey.Trim(),
                Dismiss = DismissHotkey.Trim()
            },
            Suggestions = new SuggestionSettings
            {
                Enabled = SuggestionsEnabled,
                DebounceMs = DebounceMs,
                MinCharsBeforeSuggest = MinCharsBeforeSuggest,
                MaxContextChars = MaxContextChars,
                Level1GrammarEnabled = Level1GrammarEnabled,
                Level2CompletionEnabled = Level2CompletionEnabled
            },
            Grammar = new GrammarSettings
            {
                Engine = "harper",
                Dialect = GrammarDialect,
                LlmDeepCheck = LlmDeepCheck,
                DisabledRules = SplitList(DisabledRulesText, ','),
                RequestTimeoutMs = GrammarTimeoutMs
            },
            Llm = new LlmSettings
            {
                BaseUrl = LlmBaseUrl.Trim(),
                Model = LlmModel.Trim(),
                ApiKeyEnvVar = ApiKeyEnvVar.Trim(),
                TimeoutMs = LlmTimeoutMs,
                MaxTokens = LlmMaxTokens,
                Temperature = LlmTemperature
            },
            Speech = new SpeechSettings
            {
                Model = SpeechModel.Trim(),
                Language = SpeechLanguage.Trim(),
                InsertMode = SpeechInsertMode.Trim()
            },
            BlockedProcesses = SplitList(BlockedProcessesText, '\n', '\r', ','),
            StartWithWindows = StartWithWindows
        };
    }

    private static List<string> SplitList(string text, params char[] separators)
    {
        return text
            .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
