namespace Typee.App.Resources;

using System.Globalization;
using System.Resources;

internal static class Strings
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    internal static ResourceManager ResourceManager =>
        _resourceManager ??= new ResourceManager("Typee.App.Resources.Strings", typeof(Strings).Assembly);

    internal static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    internal static string AppName => Get(nameof(AppName));
    internal static string Suggestion_GrammarFix => Get(nameof(Suggestion_GrammarFix));
    internal static string Suggestion_Completion => Get(nameof(Suggestion_Completion));
    internal static string Suggestion_List => Get(nameof(Suggestion_List));
    internal static string Suggestion_PromptAssist => Get(nameof(Suggestion_PromptAssist));
    internal static string Suggestion_Hint => Get(nameof(Suggestion_Hint));
    internal static string Suggestion_Wordmark => Get(nameof(Suggestion_Wordmark));
    internal static string Suggestion_OptionAutomationFormat => Get(nameof(Suggestion_OptionAutomationFormat));
    internal static string Tray_PauseSuggestions => Get(nameof(Tray_PauseSuggestions));
    internal static string Tray_EnableSuggestions => Get(nameof(Tray_EnableSuggestions));
    internal static string Tray_Settings => Get(nameof(Tray_Settings));
    internal static string Tray_OpenLogs => Get(nameof(Tray_OpenLogs));
    internal static string Tray_Diagnostics => Get(nameof(Tray_Diagnostics));
    internal static string Tray_ShowDummySuggestion => Get(nameof(Tray_ShowDummySuggestion));
    internal static string Tray_AutoDummyOnPause => Get(nameof(Tray_AutoDummyOnPause));
    internal static string Tray_ShowKeyActivity => Get(nameof(Tray_ShowKeyActivity));
    internal static string Tray_ShowCaretMarker => Get(nameof(Tray_ShowCaretMarker));
    internal static string Tray_Exit => Get(nameof(Tray_Exit));
    internal static string Diagnostics_Title => Get(nameof(Diagnostics_Title));
    internal static string Diagnostics_Cpu => Get(nameof(Diagnostics_Cpu));
    internal static string Diagnostics_Ram => Get(nameof(Diagnostics_Ram));
    internal static string Diagnostics_Hook => Get(nameof(Diagnostics_Hook));
    internal static string Diagnostics_LastReinstall => Get(nameof(Diagnostics_LastReinstall));
    internal static string Diagnostics_CaretMethod => Get(nameof(Diagnostics_CaretMethod));
    internal static string Diagnostics_Harper => Get(nameof(Diagnostics_Harper));
    internal static string Diagnostics_Llm => Get(nameof(Diagnostics_Llm));
    internal static string Diagnostics_RecentErrors => Get(nameof(Diagnostics_RecentErrors));
    internal static string Diagnostics_Never => Get(nameof(Diagnostics_Never));
    internal static string Diagnostics_Installed => Get(nameof(Diagnostics_Installed));
    internal static string Diagnostics_NotInstalled => Get(nameof(Diagnostics_NotInstalled));
    internal static string Overlay_AutomationName => Get(nameof(Overlay_AutomationName));

    private static string Get(string name) =>
        ResourceManager.GetString(name, _resourceCulture) ?? name;
}
