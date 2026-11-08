using System.Windows;
using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Settings;
using Typee.Core.Typing;
using Typee.Platform.Windows.Keyboard;

namespace Typee.App.Services;

/// <summary>
/// Registers the global hotkeys from settings, re-registers on settings hot-reload,
/// applies typing debounce changes, and surfaces registration failures in the UI.
/// </summary>
public sealed class HotkeyCoordinator : IDisposable
{
    private readonly IHotkeyService _hotkeyService;
    private readonly ISettingsStore _settingsStore;
    private readonly TypingActivityService _typingActivity;
    private readonly KeyboardHookService _keyboardHook;
    private readonly ILogger<HotkeyCoordinator> _logger;
    private bool _disposed;

    public HotkeyCoordinator(
        IHotkeyService hotkeyService,
        ISettingsStore settingsStore,
        TypingActivityService typingActivity,
        KeyboardHookService keyboardHook,
        ILogger<HotkeyCoordinator> logger)
    {
        _hotkeyService = hotkeyService;
        _settingsStore = settingsStore;
        _typingActivity = typingActivity;
        _keyboardHook = keyboardHook;
        _logger = logger;
        _settingsStore.SettingsChanged += OnSettingsChanged;
        _hotkeyService.RegistrationFailed += OnRegistrationFailed;
    }

    public void OnSystemResumed()
    {
        try
        {
            _keyboardHook.Reinstall();
            ApplyFromSettings(_settingsStore.Current);
            _logger.LogInformation("Reinstalled keyboard hook and hotkeys after system resume");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recover input stack after system resume");
        }
    }

    public void ApplyFromSettings(AppSettings settings)
    {
        _typingActivity.Configure(settings.Suggestions.DebounceMs);
        _hotkeyService.UnregisterAll();

        // Modifier-only gestures are registered via RegisterHotKey (authoritative).
        Register("commandBar", settings.Hotkeys.CommandBar);
        Register("sendToLastAi", settings.Hotkeys.SendToLastAi);
        Register("listStylePicker", settings.Hotkeys.ListStylePicker);
        Register("toggleChecklist", settings.Hotkeys.ToggleChecklist);

        // Push-to-talk needs key-up tracking → passive hook observation (never swallowed).
        _hotkeyService.Register("pushToTalk", settings.Hotkeys.PushToTalk,
            onPressed: () => _logger.LogInformation("Hotkey pushToTalk pressed"),
            onReleased: () => _logger.LogInformation("Hotkey pushToTalk released"));
    }

    private void Register(string id, string hotkeyText)
    {
        var result = _hotkeyService.Register(id, hotkeyText, () => _logger.LogInformation("Hotkey {HotkeyId} fired", id));
        if (result.Success)
        {
            _logger.LogInformation("Hotkey {HotkeyId} registered as {Gesture}", id, result.HotkeyText);
        }
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        try
        {
            ApplyFromSettings(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to re-register hotkeys after settings change");
        }
    }

    private void OnRegistrationFailed(object? sender, HotkeyRegistration registration)
    {
        var message = $"Hotkey “{registration.HotkeyText}” ({registration.Id}) could not be registered.\n\n{registration.Error}\n\n"
            + "Change it in Settings → Hotkeys.";
        if (Application.Current?.Dispatcher is { } dispatcher)
        {
            dispatcher.BeginInvoke(() => MessageBox.Show(message, "Typee — hotkey conflict", MessageBoxButton.OK, MessageBoxImage.Warning));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settingsStore.SettingsChanged -= OnSettingsChanged;
        _hotkeyService.RegistrationFailed -= OnRegistrationFailed;
    }
}
