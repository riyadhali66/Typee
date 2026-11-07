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

    private readonly Dictionary<string, (Func<string> Get, Action<string> Set)> _hotkeyEditors;

    public SettingsWindowViewModel(ISettingsStore settingsStore, ILogger logger)
    {
        _settingsStore = settingsStore;
        _logger = logger;

        _hotkeyEditors = new Dictionary<string, (Func<string>, Action<string>)>(StringComparer.Ordinal)
        {
            [nameof(CommandBarHotkey)] = (() => CommandBarHotkey, v => CommandBarHotkey = v),
            [nameof(SendToLastAiHotkey)] = (() => SendToLastAiHotkey, v => SendToLastAiHotkey = v),
            [nameof(ListStylePickerHotkey)] = (() => ListStylePickerHotkey, v => ListStylePickerHotkey = v),
            [nameof(ToggleChecklistHotkey)] = (() => ToggleChecklistHotkey, v => ToggleChecklistHotkey = v),
            [nameof(PushToTalkHotkey)] = (() => PushToTalkHotkey, v => PushToTalkHotkey = v),
            [nameof(AcceptSuggestionHotkey)] = (() => AcceptSuggestionHotkey, v => AcceptSuggestionHotkey = v),
            [nameof(DismissHotkey)] = (() => DismissHotkey, v => DismissHotkey = v)
        };

        LoadFrom(settingsStore.Current);
    }

    // --- Hotkeys tab ---

    [ObservableProperty]
    private string _commandBarHotkey = "Ctrl+Shift+Space";

    [ObservableProperty]
    private string _sendToLastAiHotkey = "Ctrl+Alt+Enter";

    [ObservableProperty]
    private string _listStylePickerHotkey = "Ctrl+Alt+L";

    [ObservableProperty]
    private string _toggleChecklistHotkey = "Ctrl+Alt+X";

    [ObservableProperty]
    private string _pushToTalkHotkey = "Ctrl+Alt+M";

    [ObservableProperty]
    private string _acceptSuggestionHotkey = "Tab";

    [ObservableProperty]
    private string _dismissHotkey = "Escape";

    public IReadOnlyList<(string Label, string PropertyName)> HotkeyRows { get; } =
    [
        ("Command bar", "CommandBarHotkey"),
        ("Send to last AI target", "SendToLastAiHotkey"),
        ("List style picker", "ListStylePickerHotkey"),
        ("Toggle checklist item", "ToggleChecklistHotkey"),
        ("Push to talk (hold)", "PushToTalkHotkey"),
        ("Accept suggestion", "AcceptSuggestionHotkey"),
        ("Dismiss", "DismissHotkey")
    ];

    // --- General tab ---

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
    private string _speechModel = "base.en";

    [ObservableProperty]
    private string _speechLanguage = "en";

    [ObservableProperty]
    private string _speechInsertMode = "SendInput";

    [ObservableProperty]
    private string _translateTargetsText = "English, Arabic";

    [ObservableProperty]
    private string _blockedProcessesText = string.Empty;

    [ObservableProperty]
    private bool _startWithWindows;

    // --- LLM tab ---

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

    // --- Status / capture ---

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _capturingHotkeyTarget = string.Empty;

    public ObservableCollection<string> Dialects { get; } = ["American", "British", "Australian", "Canadian"];

    public void BeginHotkeyCapture(string target)
    {
        CapturingHotkeyTarget = target;
        var label = HotkeyRows.FirstOrDefault(row => row.PropertyName == target).Label;
        StatusMessage = $"Press a key combination for {label}… (Esc cancels capture)";
    }

    public bool TryApplyCapturedHotkey(string hotkeyText)
    {
        if (string.IsNullOrWhiteSpace(CapturingHotkeyTarget))
        {
            return false;
        }

        if (!_hotkeyEditors.TryGetValue(CapturingHotkeyTarget, out var editor))
        {
            return false;
        }

        // Spec §5: never use Alt+Space (Windows system menu).
        if (string.Equals(hotkeyText, "Alt+Space", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Alt+Space is reserved for the Windows system menu — choose another combination";
            return false;
        }

        editor.Set(hotkeyText);
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
            // Clone the store's current settings so sections without UI (button,
            // smartLists, promptAssist, snippets) survive a save from this window.
            var settings = _settingsStore.Current;
            ApplyTo(settings);
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
        CommandBarHotkey = settings.Hotkeys.CommandBar;
        SendToLastAiHotkey = settings.Hotkeys.SendToLastAi;
        ListStylePickerHotkey = settings.Hotkeys.ListStylePicker;
        ToggleChecklistHotkey = settings.Hotkeys.ToggleChecklist;
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

        TranslateTargetsText = string.Join(", ", settings.TranslateTargets);
        BlockedProcessesText = string.Join(Environment.NewLine, settings.BlockedProcesses);
        StartWithWindows = settings.StartWithWindows;
    }

    private void ApplyTo(AppSettings settings)
    {
        settings.Hotkeys.CommandBar = CommandBarHotkey.Trim();
        settings.Hotkeys.SendToLastAi = SendToLastAiHotkey.Trim();
        settings.Hotkeys.ListStylePicker = ListStylePickerHotkey.Trim();
        settings.Hotkeys.ToggleChecklist = ToggleChecklistHotkey.Trim();
        settings.Hotkeys.PushToTalk = PushToTalkHotkey.Trim();
        settings.Hotkeys.AcceptSuggestion = AcceptSuggestionHotkey.Trim();
        settings.Hotkeys.Dismiss = DismissHotkey.Trim();

        settings.Suggestions.Enabled = SuggestionsEnabled;
        settings.Suggestions.DebounceMs = DebounceMs;
        settings.Suggestions.MinCharsBeforeSuggest = MinCharsBeforeSuggest;
        settings.Suggestions.MaxContextChars = MaxContextChars;
        settings.Suggestions.Level1GrammarEnabled = Level1GrammarEnabled;
        settings.Suggestions.Level2CompletionEnabled = Level2CompletionEnabled;

        settings.Grammar.Dialect = GrammarDialect;
        settings.Grammar.LlmDeepCheck = LlmDeepCheck;
        settings.Grammar.DisabledRules = SplitList(DisabledRulesText, ',');
        settings.Grammar.RequestTimeoutMs = GrammarTimeoutMs;

        settings.Llm.BaseUrl = LlmBaseUrl.Trim();
        settings.Llm.Model = LlmModel.Trim();
        settings.Llm.ApiKeyEnvVar = ApiKeyEnvVar.Trim();
        settings.Llm.TimeoutMs = LlmTimeoutMs;
        settings.Llm.MaxTokens = LlmMaxTokens;
        settings.Llm.Temperature = LlmTemperature;

        settings.Speech.Model = SpeechModel.Trim();
        settings.Speech.Language = SpeechLanguage.Trim();
        settings.Speech.InsertMode = SpeechInsertMode.Trim();

        settings.TranslateTargets = SplitList(TranslateTargetsText, ',');
        settings.BlockedProcesses = SplitList(BlockedProcessesText, '\n', '\r', ',');
        settings.StartWithWindows = StartWithWindows;
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
