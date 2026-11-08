using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Input;
using Typee.Platform.Windows.Interop;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.Keyboard;

/// <summary>
/// Hotkeys. Modifier gestures without release tracking are registered via RegisterHotKey on a
/// message-only window (authoritative — the system swallows the key). Bare keys (Tab, Escape)
/// and gestures needing key-release tracking (push-to-talk) are observed passively through
/// <see cref="IKeyboardEvents"/> and are never swallowed.
/// Callbacks fire on background threads — marshal to the UI thread where needed.
/// </summary>
public sealed class HotkeyService : IHotkeyService, IDisposable
{
    private readonly IKeyboardEvents _keyboardEvents;
    private readonly ILogger<HotkeyService> _logger;
    private readonly MessageWindow _window;
    private readonly object _gate = new();
    private readonly Dictionary<string, Registration> _registrations = [];
    private readonly HashSet<int> _usedHotkeyIds = [];
    private readonly Dictionary<int, string> _hotkeyIdToRegistrationId = [];
    private readonly List<PassiveGesture> _passiveGestures = [];
    private readonly HashSet<int> _pressedVirtualKeys = [];
    private int _nextHotkeyId = 1; // RegisterHotKey ids must be in 0x0000..0xBFFF
    private bool _keyboardSubscribed;
    private bool _disposed;

    public HotkeyService(IKeyboardEvents keyboardEvents, ILogger<HotkeyService> logger)
    {
        _keyboardEvents = keyboardEvents;
        _logger = logger;
        _window = new MessageWindow(logger);
        _window.SetHotkeyHandler(OnWmHotkey);
    }

    public event EventHandler<HotkeyRegistration>? RegistrationFailed;

    public HotkeyRegistration Register(string id, string hotkeyText, Action onPressed, Action? onReleased = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!HotkeyGesture.TryParse(hotkeyText, out var gesture, out var parseError))
        {
            return Fail(id, hotkeyText, parseError);
        }

        HotkeyRegistration result;
        lock (_gate)
        {
            UnregisterLocked(id);

            var conflicting = _registrations.Values.FirstOrDefault(r =>
                r.Gesture.VirtualKey == gesture.VirtualKey && r.Gesture.Modifiers == gesture.Modifiers);
            if (conflicting is not null)
            {
                result = Fail(id, hotkeyText, $"'{gesture.NormalizedText}' is already registered as '{conflicting.Id}'.");
            }
            else if (gesture.HasModifiers && onReleased is null)
            {
                result = RegisterSystemHotkeyLocked(id, hotkeyText, gesture, onPressed);
            }
            else
            {
                result = RegisterPassiveLocked(id, hotkeyText, gesture, onPressed, onReleased);
            }

            if (result.Success)
            {
                _registrations[id] = new Registration(id, gesture, onPressed, onReleased);
            }
        }

        return result;
    }

    public void Unregister(string id)
    {
        lock (_gate)
        {
            UnregisterLocked(id);
        }
    }

    public void UnregisterAll()
    {
        lock (_gate)
        {
            foreach (var id in _registrations.Keys.ToArray())
            {
                UnregisterLocked(id);
            }
        }
    }

    private HotkeyRegistration RegisterSystemHotkeyLocked(string id, string hotkeyText, HotkeyGesture gesture, Action onPressed)
    {
        if (_window.Handle == 0)
        {
            return Fail(id, hotkeyText, "Hotkey window could not be created.");
        }

        var hotkeyId = _nextHotkeyId++;
        var registered = false;
        int? win32Error = null;

        var ran = _window.TryRun(() =>
        {
            registered = RegisterHotKey(_window.Handle, hotkeyId, ToRegisterModifiers(gesture.Modifiers), (uint)gesture.VirtualKey);
            win32Error = registered ? null : Marshal.GetLastWin32Error();
        }, TimeSpan.FromSeconds(3));

        if (!ran)
        {
            return Fail(id, hotkeyText, "Hotkey thread did not respond.");
        }

        if (!registered)
        {
            var error = win32Error == ErrorHotkeyAlreadyRegistered
                ? $"'{gesture.NormalizedText}' is already registered by another application."
                : $"'{gesture.NormalizedText}' could not be registered (Windows error {win32Error}).";
            return Fail(id, hotkeyText, error);
        }

        _usedHotkeyIds.Add(hotkeyId);
        _hotkeyIdToRegistrationId[hotkeyId] = id;
        return HotkeyRegistration.Ok(id, hotkeyText);
    }

    private HotkeyRegistration RegisterPassiveLocked(string id, string hotkeyText, HotkeyGesture gesture, Action onPressed, Action? onReleased)
    {
        _passiveGestures.Add(new PassiveGesture(id, gesture, onPressed, onReleased, IsGestureDown: false));
        if (!_keyboardSubscribed)
        {
            _keyboardEvents.KeyEvent += OnKeyEvent;
            _keyboardSubscribed = true;
        }

        return HotkeyRegistration.Ok(id, hotkeyText);
    }

    private void UnregisterLocked(string id)
    {
        if (!_registrations.Remove(id, out var registration))
        {
            return;
        }

        var passiveIndex = _passiveGestures.FindIndex(p => p.Id == id);
        if (passiveIndex >= 0)
        {
            _passiveGestures.RemoveAt(passiveIndex);
        }

        var hotkeyId = _hotkeyIdToRegistrationId.FirstOrDefault(kv => kv.Value == id).Key;
        if (hotkeyId != 0 && _window.Handle != 0)
        {
            _window.TryRun(() =>
            {
                _ = UnregisterHotKey(_window.Handle, hotkeyId);
            }, TimeSpan.FromSeconds(3));
            _ = _hotkeyIdToRegistrationId.Remove(hotkeyId);
            _usedHotkeyIds.Remove(hotkeyId);
        }
    }

    private void OnWmHotkey(int hotkeyId)
    {
        Action? onPressed = null;
        var id = string.Empty;
        lock (_gate)
        {
            if (_hotkeyIdToRegistrationId.TryGetValue(hotkeyId, out var registrationId)
                && _registrations.TryGetValue(registrationId, out var registration))
            {
                onPressed = registration.OnPressed;
                id = registration.Id;
            }
        }

        if (onPressed is not null)
        {
            SafeInvoke(id, onPressed);
        }
    }

    private void OnKeyEvent(object? sender, KeyboardEvent e)
    {
        List<(string Id, Action Pressed, Action? Released)>? edges = null;
        lock (_gate)
        {
            if (e.IsKeyDown)
            {
                _pressedVirtualKeys.Add(e.VirtualKey);
            }
            else
            {
                _pressedVirtualKeys.Remove(e.VirtualKey);
            }

            for (var i = 0; i < _passiveGestures.Count; i++)
            {
                var passive = _passiveGestures[i];
                var isDown = IsGestureSatisfied(passive.Gesture);
                if (isDown == passive.IsGestureDown)
                {
                    continue;
                }

                _passiveGestures[i] = passive with { IsGestureDown = isDown };
                (edges ??= []).Add(isDown ? (passive.Id, passive.OnPressed, null) : (passive.Id, () => { }, passive.OnReleased));
            }
        }

        if (edges is null)
        {
            return;
        }

        foreach (var (id, pressed, released) in edges)
        {
            if (released is null)
            {
                SafeInvoke(id, pressed);
            }
            else
            {
                try
                {
                    released();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Hotkey release handler for {HotkeyId} threw", id);
                }
            }
        }
    }

    private bool IsGestureSatisfied(HotkeyGesture gesture)
    {
        if (!_pressedVirtualKeys.Contains(gesture.VirtualKey))
        {
            return false;
        }

        // The LL hook reports side-specific modifier codes (VK_LCONTROL …); accept either side.
        if ((gesture.Modifiers & KeyModifiers.Ctrl) != 0
            && !_pressedVirtualKeys.ContainsAny(VirtualKeys.Control, VirtualKeys.LControl, VirtualKeys.RControl))
        {
            return false;
        }

        if ((gesture.Modifiers & KeyModifiers.Shift) != 0
            && !_pressedVirtualKeys.ContainsAny(VirtualKeys.Shift, VirtualKeys.LShift, VirtualKeys.RShift))
        {
            return false;
        }

        if ((gesture.Modifiers & KeyModifiers.Alt) != 0
            && !_pressedVirtualKeys.ContainsAny(VirtualKeys.Alt, VirtualKeys.LAlt, VirtualKeys.RAlt))
        {
            return false;
        }

        if ((gesture.Modifiers & KeyModifiers.Win) != 0 && !_pressedVirtualKeys.ContainsAny(VirtualKeys.LWin, VirtualKeys.RWin))
        {
            return false;
        }

        return true;
    }

    private static uint ToRegisterModifiers(KeyModifiers modifiers)
    {
        uint result = 0;
        if ((modifiers & KeyModifiers.Alt) != 0)
        {
            result |= ModAlt;
        }

        if ((modifiers & KeyModifiers.Ctrl) != 0)
        {
            result |= ModControl;
        }

        if ((modifiers & KeyModifiers.Shift) != 0)
        {
            result |= ModShift;
        }

        if ((modifiers & KeyModifiers.Win) != 0)
        {
            result |= ModWin;
        }

        return result;
    }

    private void SafeInvoke(string id, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hotkey handler for {HotkeyId} threw", id);
        }
    }

    private HotkeyRegistration Fail(string id, string hotkeyText, string error)
    {
        var result = HotkeyRegistration.Fail(id, hotkeyText, error);
        _logger.LogWarning("Hotkey registration failed: {Id} '{Text}': {Error}", id, hotkeyText, error);
        RegistrationFailed?.Invoke(this, result);
        return result;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_keyboardSubscribed)
        {
            _keyboardEvents.KeyEvent -= OnKeyEvent;
        }

        UnregisterAll();
        _window.Dispose();
    }

    private sealed record Registration(string Id, HotkeyGesture Gesture, Action OnPressed, Action? OnReleased);

    private sealed record PassiveGesture(string Id, HotkeyGesture Gesture, Action OnPressed, Action? OnReleased, bool IsGestureDown);
}

internal static class KeySetExtensions
{
    public static bool ContainsAny(this HashSet<int> set, params int[] keys)
    {
        foreach (var key in keys)
        {
            if (set.Contains(key))
            {
                return true;
            }
        }

        return false;
    }
}
