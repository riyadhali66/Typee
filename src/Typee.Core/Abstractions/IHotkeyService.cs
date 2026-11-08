namespace Typee.Core.Abstractions;

/// <summary>Outcome of a hotkey registration.</summary>
public sealed record HotkeyRegistration(string Id, string HotkeyText, bool Success, string? Error)
{
    public static HotkeyRegistration Ok(string id, string hotkeyText) => new(id, hotkeyText, true, null);

    public static HotkeyRegistration Fail(string id, string hotkeyText, string error) =>
        new(id, hotkeyText, false, error);
}

/// <summary>
/// Global hotkeys. Gestures with modifiers are registered via RegisterHotKey (authoritative,
/// key presses are swallowed by the system); bare keys and gestures that need key-release
/// tracking (push-to-talk) are observed passively through the low-level hook (never swallowed).
/// Callbacks fire on a background thread — marshal to the UI thread where needed.
/// </summary>
public interface IHotkeyService
{
    /// <summary>Raised when a registration fails, for surfacing in the UI.</summary>
    event EventHandler<HotkeyRegistration>? RegistrationFailed;

    /// <summary>Registers (or replaces) a hotkey. Conflict-checked; failures are reported, never thrown.</summary>
    HotkeyRegistration Register(string id, string hotkeyText, Action onPressed, Action? onReleased = null);

    void Unregister(string id);

    void UnregisterAll();
}
