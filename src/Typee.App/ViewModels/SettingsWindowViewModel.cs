using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Typee.App.Services;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Core.Phrases;
using Typee.Core.Settings;

namespace Typee.App.ViewModels;

public partial class SettingsWindowViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly ISettingsStore _settingsStore;
    private readonly IAppPaths _paths;
    private readonly IPhraseService _phrases;
    private readonly IUserDictionaryRepository _dictionary;
    private readonly ITypeeDatabase _database;
    private readonly ILogger _logger;

    private readonly Dictionary<string, (Func<string> Get, Action<string> Set)> _hotkeyEditors;

    public SettingsWindowViewModel(
        ISettingsStore settingsStore,
        IAppPaths paths,
        IPhraseService phrases,
        IUserDictionaryRepository dictionary,
        ITypeeDatabase database,
        ILogger logger)
    {
        _settingsStore = settingsStore;
        _paths = paths;
        _phrases = phrases;
        _dictionary = dictionary;
        _database = database;
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
        RefreshLoadNotices();
        _ = RefreshLearnedDataAsync();
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

    [ObservableProperty]
    private bool _pauseInFullscreen = true;

    [ObservableProperty]
    private string _uiLanguage = "auto";

    [ObservableProperty]
    private string _loadNoticesText = string.Empty;

    [ObservableProperty]
    private int _completionCacheTtlHours = 168;

    [ObservableProperty]
    private string _storageSummary =
        "settings.json / settings.backup.json (preferences only — API keys stay in environment variables); "
        + "typee.db (phrases, dictionary, completion cache, snippets, custom commands, recent instructions, "
        + "action profile memory, endpoint acknowledgments, caret method memory — process names only); "
        + "logs\\ (counts and errors, never typed text); models\\ (downloaded speech models); crash.marker while running.";

    public ObservableCollection<string> UiLanguages { get; } = ["auto", "en", "ar"];
    public ObservableCollection<PhraseRowViewModel> Phrases { get; } = [];
    public ObservableCollection<DictionaryRowViewModel> DictionaryWords { get; } = [];

    [ObservableProperty]
    private string _newPhraseText = string.Empty;

    [ObservableProperty]
    private PhraseRowViewModel? _selectedPhrase;

    [ObservableProperty]
    private string _newDictionaryWord = string.Empty;

    [ObservableProperty]
    private DictionaryRowViewModel? _selectedDictionaryWord;

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
    private async Task ExportSettingsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = "typee-settings.json"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _settingsStore.ExportAsync(dialog.FileName).ConfigureAwait(true);
            StatusMessage = "Settings exported (no API keys in file)";
        }
        catch (Exception ex)
        {
            StatusMessage = "Export failed — see logs";
            _logger.LogError(ex, "Settings export failed");
        }
    }

    [RelayCommand]
    private async Task ImportSettingsAsync()
    {
        var dialog = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _settingsStore.ImportAsync(dialog.FileName).ConfigureAwait(true);
            LoadFrom(_settingsStore.Current);
            RefreshLoadNotices();
            StatusMessage = "Settings imported";
        }
        catch (Exception ex)
        {
            StatusMessage = "Import failed — see logs";
            _logger.LogError(ex, "Settings import failed");
        }
    }

    [RelayCommand]
    private async Task ResetSettingsAsync()
    {
        if (MessageBox.Show(
                "Reset all settings to defaults?",
                "Typee Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _settingsStore.ResetToDefaultsAsync().ConfigureAwait(true);
            LoadFrom(_settingsStore.Current);
            StatusMessage = "Settings reset to defaults";
        }
        catch (Exception ex)
        {
            StatusMessage = "Reset failed — see logs";
            _logger.LogError(ex, "Settings reset failed");
        }
    }

    [RelayCommand]
    private async Task ClearLearnedDataAsync()
    {
        if (MessageBox.Show(
                "Clear all learned data from typee.db?\n\n"
                + "This removes phrases, dictionary words, completion cache, recent instructions, "
                + "action/profile memory, endpoint acknowledgments, and caret method memory.\n"
                + "Snippets, custom commands, settings, and logs are kept.",
                "Typee — Clear learned data",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _database.ClearLearnedDataAsync().ConfigureAwait(true);
            await RefreshLearnedDataAsync().ConfigureAwait(true);
            StatusMessage = "Learned data cleared";
            _logger.LogInformation("Cleared learned SQLite data from Privacy tab");
        }
        catch (Exception ex)
        {
            StatusMessage = "Clear learned data failed — see logs";
            _logger.LogError(ex, "Failed to clear learned data");
        }
    }

    [RelayCommand]
    private async Task DeleteAllDataAsync()
    {
        if (MessageBox.Show(
                "Delete ALL Typee data under %LOCALAPPDATA%\\Typee?\n\n"
                + "This removes settings, typee.db (including snippets/commands), logs, crash marker, "
                + "and reloads defaults. API keys in environment variables are untouched.",
                "Typee — Delete all data",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _database.DeleteDatabaseAsync().ConfigureAwait(true);
            await ClearLocalUserDataAsync(includeSettings: true).ConfigureAwait(true);
            await _settingsStore.ResetToDefaultsAsync().ConfigureAwait(true);
            LoadFrom(_settingsStore.Current);
            await RefreshLearnedDataAsync().ConfigureAwait(true);
            StatusMessage = "All local Typee data deleted";
            _logger.LogInformation("Deleted all local Typee data from Privacy tab");
        }
        catch (Exception ex)
        {
            StatusMessage = "Delete all data failed — see logs";
            _logger.LogError(ex, "Failed to delete all Typee data");
        }
    }

    [RelayCommand]
    private async Task AddPhraseAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPhraseText))
        {
            return;
        }

        try
        {
            await _phrases.AddManualAsync(NewPhraseText).ConfigureAwait(true);
            NewPhraseText = string.Empty;
            await RefreshPhrasesAsync().ConfigureAwait(true);
            StatusMessage = "Phrase added";
        }
        catch (Exception ex)
        {
            StatusMessage = "Add phrase failed — see logs";
            _logger.LogError(ex, "Failed to add phrase");
        }
    }

    [RelayCommand]
    private async Task DeletePhraseAsync()
    {
        if (SelectedPhrase is null)
        {
            return;
        }

        try
        {
            await _phrases.DeleteAsync(SelectedPhrase.Id).ConfigureAwait(true);
            await RefreshPhrasesAsync().ConfigureAwait(true);
            StatusMessage = "Phrase deleted";
        }
        catch (Exception ex)
        {
            StatusMessage = "Delete phrase failed — see logs";
            _logger.LogError(ex, "Failed to delete phrase id={PhraseId}", SelectedPhrase.Id);
        }
    }

    [RelayCommand]
    private async Task ExportPhrasesAsync()
    {
        var dialog = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "typee-phrases.json" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var all = await _phrases.GetAllAsync().ConfigureAwait(true);
            var dto = all.Select(static p => new PhraseExportDto
            {
                Text = p.Text,
                TriggerPrefix = p.TriggerPrefix,
                UseCount = p.UseCount,
                Source = p.Source.ToString().ToLowerInvariant()
            }).ToList();
            await File.WriteAllTextAsync(dialog.FileName, JsonSerializer.Serialize(dto, JsonOptions)).ConfigureAwait(true);
            StatusMessage = $"Exported {dto.Count} phrases";
            _logger.LogInformation("Exported phrases count={Count}", dto.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = "Export phrases failed — see logs";
            _logger.LogError(ex, "Failed to export phrases");
        }
    }

    [RelayCommand]
    private async Task ImportPhrasesAsync()
    {
        var dialog = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(dialog.FileName).ConfigureAwait(true);
            var dto = JsonSerializer.Deserialize<List<PhraseExportDto>>(json, JsonOptions) ?? [];
            var phrases = dto
                .Where(static d => !string.IsNullOrWhiteSpace(d.Text))
                .Select(static d => new Phrase
                {
                    Text = d.Text.Trim(),
                    TriggerPrefix = string.IsNullOrWhiteSpace(d.TriggerPrefix)
                        ? PhraseTrie.DeriveTriggerPrefix(d.Text)
                        : d.TriggerPrefix.Trim(),
                    UseCount = Math.Max(0, d.UseCount),
                    Source = string.Equals(d.Source, "accepted", StringComparison.OrdinalIgnoreCase)
                        ? PhraseSource.Accepted
                        : PhraseSource.Manual
                });
            await _phrases.ImportAsync(phrases).ConfigureAwait(true);
            await RefreshPhrasesAsync().ConfigureAwait(true);
            StatusMessage = "Phrases imported";
            _logger.LogInformation("Imported phrases from file");
        }
        catch (Exception ex)
        {
            StatusMessage = "Import phrases failed — see logs";
            _logger.LogError(ex, "Failed to import phrases");
        }
    }

    [RelayCommand]
    private async Task AddDictionaryWordAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDictionaryWord))
        {
            return;
        }

        try
        {
            await _dictionary.UpsertAsync(NewDictionaryWord.Trim(), DateTimeOffset.UtcNow).ConfigureAwait(true);
            NewDictionaryWord = string.Empty;
            await RefreshDictionaryAsync().ConfigureAwait(true);
            StatusMessage = "Dictionary word added";
        }
        catch (Exception ex)
        {
            StatusMessage = "Add dictionary word failed — see logs";
            _logger.LogError(ex, "Failed to add dictionary word");
        }
    }

    [RelayCommand]
    private async Task DeleteDictionaryWordAsync()
    {
        if (SelectedDictionaryWord is null)
        {
            return;
        }

        try
        {
            var word = SelectedDictionaryWord.Word;
            await _dictionary.DeleteAsync(word).ConfigureAwait(true);
            await RefreshDictionaryAsync().ConfigureAwait(true);
            StatusMessage = "Dictionary word deleted";
            _logger.LogInformation("Deleted dictionary word");
        }
        catch (Exception ex)
        {
            StatusMessage = "Delete dictionary word failed — see logs";
            _logger.LogError(ex, "Failed to delete dictionary word");
        }
    }

    [RelayCommand]
    private async Task ExportDictionaryAsync()
    {
        var dialog = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "typee-dictionary.json" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var all = await _dictionary.GetAllAsync().ConfigureAwait(true);
            var dto = all.Select(static w => new DictionaryExportDto { Word = w.Word }).ToList();
            await File.WriteAllTextAsync(dialog.FileName, JsonSerializer.Serialize(dto, JsonOptions)).ConfigureAwait(true);
            StatusMessage = $"Exported {dto.Count} dictionary words";
            _logger.LogInformation("Exported dictionary count={Count}", dto.Count);
        }
        catch (Exception ex)
        {
            StatusMessage = "Export dictionary failed — see logs";
            _logger.LogError(ex, "Failed to export dictionary");
        }
    }

    [RelayCommand]
    private async Task ImportDictionaryAsync()
    {
        var dialog = new OpenFileDialog { Filter = "JSON (*.json)|*.json" };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(dialog.FileName).ConfigureAwait(true);
            var dto = JsonSerializer.Deserialize<List<DictionaryExportDto>>(json, JsonOptions) ?? [];
            var now = DateTimeOffset.UtcNow;
            var count = 0;
            foreach (var item in dto)
            {
                if (string.IsNullOrWhiteSpace(item.Word))
                {
                    continue;
                }

                await _dictionary.UpsertAsync(item.Word.Trim(), now).ConfigureAwait(true);
                count++;
            }

            await RefreshDictionaryAsync().ConfigureAwait(true);
            StatusMessage = $"Imported {count} dictionary words";
            _logger.LogInformation("Imported dictionary count={Count}", count);
        }
        catch (Exception ex)
        {
            StatusMessage = "Import dictionary failed — see logs";
            _logger.LogError(ex, "Failed to import dictionary");
        }
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        try
        {
            var settings = await _settingsStore.LoadAsync().ConfigureAwait(true);
            LoadFrom(settings);
            RefreshLoadNotices();
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
        CompletionCacheTtlHours = settings.Suggestions.CompletionCacheTtlHours;

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
        PauseInFullscreen = settings.PauseInFullscreen;
        UiLanguage = settings.UiLanguage;
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
        settings.Suggestions.CompletionCacheTtlHours = CompletionCacheTtlHours;

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
        settings.PauseInFullscreen = PauseInFullscreen;
        settings.UiLanguage = UiLanguage.Trim();
        settings.SchemaVersion = SettingsMigrator.CurrentSchemaVersion;
    }

    private void RefreshLoadNotices()
    {
        var notices = _settingsStore.LastLoadNotices;
        LoadNoticesText = notices.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, notices);
    }

    private async Task ClearLocalUserDataAsync(bool includeSettings)
    {
        try
        {
            if (includeSettings && File.Exists(_paths.SettingsFilePath))
            {
                File.Delete(_paths.SettingsFilePath);
            }

            if (File.Exists(_paths.SettingsBackupFilePath))
            {
                File.Delete(_paths.SettingsBackupFilePath);
            }

            CrashDiagnostics.ClearMarker(_paths);

            if (Directory.Exists(_paths.LogsDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(_paths.LogsDirectory))
                {
                    File.Delete(file);
                }
            }

            _logger.LogInformation("Cleared local user data (settings/logs/marker)");
            await _settingsStore.LoadAsync().ConfigureAwait(true);
            LoadFrom(_settingsStore.Current);
            StatusMessage = "Local data cleared";
        }
        catch (Exception ex)
        {
            StatusMessage = "Clear failed — see logs";
            _logger.LogError(ex, "Failed to clear local user data");
        }
    }

    private async Task RefreshLearnedDataAsync()
    {
        await RefreshPhrasesAsync().ConfigureAwait(true);
        await RefreshDictionaryAsync().ConfigureAwait(true);
    }

    private async Task RefreshPhrasesAsync()
    {
        try
        {
            var all = await _phrases.GetAllAsync().ConfigureAwait(true);
            Phrases.Clear();
            foreach (var phrase in all)
            {
                Phrases.Add(new PhraseRowViewModel(phrase));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh phrases list");
        }
    }

    private async Task RefreshDictionaryAsync()
    {
        try
        {
            var all = await _dictionary.GetAllAsync().ConfigureAwait(true);
            DictionaryWords.Clear();
            foreach (var word in all)
            {
                DictionaryWords.Add(new DictionaryRowViewModel(word));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh dictionary list");
        }
    }

    private static List<string> SplitList(string text, params char[] separators)
    {
        return text
            .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed class PhraseExportDto
    {
        public string Text { get; set; } = string.Empty;
        public string TriggerPrefix { get; set; } = string.Empty;
        public int UseCount { get; set; }
        public string Source { get; set; } = "manual";
    }

    private sealed class DictionaryExportDto
    {
        public string Word { get; set; } = string.Empty;
    }
}
