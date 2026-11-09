using Typee.Core.Input;

namespace Typee.Core.Settings;

public static class SettingsValidator
{
    /// <summary>Repairs invalid fields to defaults and returns human-readable field names that were reset.</summary>
    public static IReadOnlyList<string> ValidateAndRepair(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var reset = new List<string>();
        var defaults = AppSettings.CreateDefault();

        RepairHotkey(settings.Hotkeys.CommandBar, defaults.Hotkeys.CommandBar, v => settings.Hotkeys.CommandBar = v, "hotkeys.commandBar", reset);
        RepairHotkey(settings.Hotkeys.SendToLastAi, defaults.Hotkeys.SendToLastAi, v => settings.Hotkeys.SendToLastAi = v, "hotkeys.sendToLastAi", reset);
        RepairHotkey(settings.Hotkeys.ListStylePicker, defaults.Hotkeys.ListStylePicker, v => settings.Hotkeys.ListStylePicker = v, "hotkeys.listStylePicker", reset);
        RepairHotkey(settings.Hotkeys.ToggleChecklist, defaults.Hotkeys.ToggleChecklist, v => settings.Hotkeys.ToggleChecklist = v, "hotkeys.toggleChecklist", reset);
        RepairHotkey(settings.Hotkeys.PushToTalk, defaults.Hotkeys.PushToTalk, v => settings.Hotkeys.PushToTalk = v, "hotkeys.pushToTalk", reset);
        RepairHotkey(settings.Hotkeys.AcceptSuggestion, defaults.Hotkeys.AcceptSuggestion, v => settings.Hotkeys.AcceptSuggestion = v, "hotkeys.acceptSuggestion", reset);
        RepairHotkey(settings.Hotkeys.Dismiss, defaults.Hotkeys.Dismiss, v => settings.Hotkeys.Dismiss = v, "hotkeys.dismiss", reset);

        if (!Uri.TryCreate(settings.Llm.BaseUrl, UriKind.Absolute, out _))
        {
            settings.Llm.BaseUrl = defaults.Llm.BaseUrl;
            reset.Add("llm.baseUrl");
        }

        if (string.IsNullOrWhiteSpace(settings.Llm.Model))
        {
            settings.Llm.Model = defaults.Llm.Model;
            reset.Add("llm.model");
        }

        if (string.IsNullOrWhiteSpace(settings.Llm.ApiKeyEnvVar))
        {
            settings.Llm.ApiKeyEnvVar = defaults.Llm.ApiKeyEnvVar;
            reset.Add("llm.apiKeyEnvVar");
        }

        if (settings.Suggestions.DebounceMs is < 50 or > 10_000)
        {
            settings.Suggestions.DebounceMs = defaults.Suggestions.DebounceMs;
            reset.Add("suggestions.debounceMs");
        }

        if (settings.Suggestions.MinCharsBeforeSuggest is < 1 or > 10_000)
        {
            settings.Suggestions.MinCharsBeforeSuggest = defaults.Suggestions.MinCharsBeforeSuggest;
            reset.Add("suggestions.minCharsBeforeSuggest");
        }

        if (settings.Suggestions.MaxContextChars is < 32 or > 20_000)
        {
            settings.Suggestions.MaxContextChars = defaults.Suggestions.MaxContextChars;
            reset.Add("suggestions.maxContextChars");
        }

        if (settings.Suggestions.CompletionCacheTtlHours is < 1 or > 8760)
        {
            settings.Suggestions.CompletionCacheTtlHours = defaults.Suggestions.CompletionCacheTtlHours;
            reset.Add("suggestions.completionCacheTtlHours");
        }

        if (settings.Grammar.RequestTimeoutMs is < 50 or > 10_000)
        {
            settings.Grammar.RequestTimeoutMs = defaults.Grammar.RequestTimeoutMs;
            reset.Add("grammar.requestTimeoutMs");
        }

        if (settings.Llm.TimeoutMs is < 500 or > 120_000)
        {
            settings.Llm.TimeoutMs = defaults.Llm.TimeoutMs;
            reset.Add("llm.timeoutMs");
        }

        if (settings.Llm.MaxTokens is < 1 or > 2048)
        {
            settings.Llm.MaxTokens = defaults.Llm.MaxTokens;
            reset.Add("llm.maxTokens");
        }

        if (settings.PromptAssist.DetectionThreshold is < 0 or > 1)
        {
            settings.PromptAssist.DetectionThreshold = defaults.PromptAssist.DetectionThreshold;
            reset.Add("promptAssist.detectionThreshold");
        }

        if (string.IsNullOrWhiteSpace(settings.UiLanguage)
            || settings.UiLanguage is not ("auto" or "en" or "ar"))
        {
            settings.UiLanguage = defaults.UiLanguage;
            reset.Add("uiLanguage");
        }

        return reset;
    }

    private static void RepairHotkey(
        string value,
        string defaultValue,
        Action<string> set,
        string fieldName,
        List<string> reset)
    {
        if (HotkeyGesture.TryParse(value, out _, out _))
        {
            return;
        }

        set(defaultValue);
        reset.Add(fieldName);
    }
}
