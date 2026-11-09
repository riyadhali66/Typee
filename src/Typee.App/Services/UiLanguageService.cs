using System.Globalization;
using System.Windows;
using Typee.App.Resources;
using Typee.Core.Abstractions;
using Typee.Core.Settings;

namespace Typee.App.Services;

/// <summary>Applies <c>uiLanguage</c> (auto|en|ar) to UI culture and FlowDirection.</summary>
public sealed class UiLanguageService
{
    private readonly ISettingsStore _settingsStore;

    public UiLanguageService(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        Apply(_settingsStore.Current);
        _settingsStore.SettingsChanged += (_, settings) => Apply(settings);
    }

    public FlowDirection CurrentFlowDirection { get; private set; } = FlowDirection.LeftToRight;

    public void Apply(AppSettings settings)
    {
        var culture = ResolveCulture(settings.UiLanguage);
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Strings.Culture = culture;
        CurrentFlowDirection = culture.TextInfo.IsRightToLeft
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }

    public static CultureInfo ResolveCulture(string uiLanguage) =>
        uiLanguage switch
        {
            "en" => CultureInfo.GetCultureInfo("en"),
            "ar" => CultureInfo.GetCultureInfo("ar"),
            _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ar", StringComparison.OrdinalIgnoreCase)
                ? CultureInfo.GetCultureInfo("ar")
                : CultureInfo.GetCultureInfo("en")
        };
}
