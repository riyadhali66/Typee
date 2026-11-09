using System.Diagnostics;
using System.Threading.Channels;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Input;
using Typee.Core.Typing;
using Typee.Platform.Windows.Interop;
using Typee.Platform.Windows.Process;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.Keyboard;

/// <summary>
/// Global low-level keyboard hook on a dedicated thread with its own message loop.
/// The callback only reads the event, consults the volatile swallow flags, enqueues to a
/// bounded channel, and calls CallNextHookEx — nothing else (sub-millisecond contract).
/// Injected events (SendInput/automation) are ignored. <see cref="IKeyboardEvents.KeyEvent"/>
/// is raised on a consumer task, not the hook thread.
/// </summary>
public sealed class KeyboardHookService : IKeyboardEvents, IDisposable
{
    private const int ChannelCapacity = 8192;

    private readonly KeyboardInterceptionState _interception;
    private readonly IRawKeyboardActivityClock _rawKeyboardClock;
    private readonly ISessionLockState _sessionLockState;
    private readonly ILogger<KeyboardHookService> _logger;
    private readonly Channel<KeyboardEvent> _channel = Channel.CreateBounded<KeyboardEvent>(
        new BoundedChannelOptions(ChannelCapacity) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly object _gate = new();
    private Thread? _thread;
    private nint _hookHandle;
    private uint _hookThreadId;
    private long _droppedEvents;
    private int _lastSeenEventTick = Environment.TickCount;
    private int _lastReinstallTick;
    private Timer? _watchdogTimer;
    private volatile bool _disposing;
    private CancellationTokenSource? _consumerCts;
    private Task? _consumerTask;

    public event EventHandler? HookStoppedUnexpectedly;

    private static readonly HookProc HookProcDelegate = HookProcCallback;

    private static unsafe nint HookProcCallback(int code, nint wParam, nint lParam)
    {
#if DEBUG
        var sw = Stopwatch.StartNew();
#endif
        if (code < 0)
        {
            return CallNextHookEx(0, code, wParam, lParam);
        }

        var message = (int)wParam;
        var isKeyDown = message is WmKeyDown or WmSysKeyDown;
        if (!isKeyDown && message is not (WmKeyUp or WmSysKeyUp))
        {
            return CallNextHookEx(0, code, wParam, lParam);
        }

        var data = Unsafe.Read<KbdLlHookStruct>((void*)lParam);
        if ((data.Flags & LlkhfInjected) != 0)
        {
            return CallNextHookEx(0, code, wParam, lParam);
        }

        if (_current is not null && _current._interception.ShouldSwallow(data.VkCode, isKeyDown))
        {
            return 1; // Swallow only via the volatile flags — see KeyboardInterceptionState.
        }

        if (_current is not null)
        {
            // Snapshot modifiers with GetAsyncKeyState — fast enough for the sub-ms budget.
            var modifiers = KeyModifiers.None;
            if ((GetAsyncKeyState(VkControl) & 0x8000) != 0)
            {
                modifiers |= KeyModifiers.Ctrl;
            }

            if ((GetAsyncKeyState(VkShift) & 0x8000) != 0)
            {
                modifiers |= KeyModifiers.Shift;
            }

            if ((GetAsyncKeyState(VkMenu) & 0x8000) != 0)
            {
                modifiers |= KeyModifiers.Alt;
            }

            if ((GetAsyncKeyState(VkLWin) & 0x8000) != 0 || (GetAsyncKeyState(VkRWin) & 0x8000) != 0)
            {
                modifiers |= KeyModifiers.Win;
            }

            Interlocked.Exchange(ref _current._lastSeenEventTick, data.Time != 0 ? data.Time : Environment.TickCount);

            if (!_current._channel.Writer.TryWrite(
                    new KeyboardEvent(data.VkCode, isKeyDown, data.Flags, modifiers, (uint)data.Time, IsInjected: false)))
            {
                Interlocked.Increment(ref _current._droppedEvents);
            }
        }

#if DEBUG
        sw.Stop();
        if (sw.ElapsedMilliseconds > 1 && _current is not null)
        {
            _current._logger.LogWarning("Keyboard hook callback exceeded 1 ms ({ElapsedMicros} µs)", sw.Elapsed.TotalMicroseconds);
        }
#endif
        return CallNextHookEx(0, code, wParam, lParam);
    }

    private static KeyboardHookService? _current;

    public KeyboardHookService(
        KeyboardInterceptionState interception,
        IRawKeyboardActivityClock rawKeyboardClock,
        ISessionLockState sessionLockState,
        ILogger<KeyboardHookService> logger)
    {
        _interception = interception;
        _rawKeyboardClock = rawKeyboardClock;
        _sessionLockState = sessionLockState;
        _logger = logger;
    }

    public event EventHandler<KeyboardEvent>? KeyEvent;

    /// <summary>Installs the hook. Safe to call once; subsequent calls are no-ops when healthy.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_thread is { IsAlive: true } && _hookHandle != 0)
            {
                return;
            }

            // Alive thread with no hook is unhealthy — stop it before starting another.
            if (_thread is { IsAlive: true })
            {
                if (_hookThreadId != 0)
                {
                    _ = PostThreadMessageW(_hookThreadId, WmQuit, 0, 0);
                    _thread.Join(TimeSpan.FromSeconds(2));
                }

                _thread = null;
                _hookHandle = 0;
                _hookThreadId = 0;
            }
            else if (_thread is not null)
            {
                _thread = null;
                _hookHandle = 0;
                _hookThreadId = 0;
            }

            var ready = new ManualResetEventSlim(false);
            _thread = new Thread(HookThreadProc) { Name = "Typee.KeyboardHook", IsBackground = true };
            _thread.Start(ready);
            ready.Wait(TimeSpan.FromSeconds(5));
        }

        _consumerCts ??= new CancellationTokenSource();
        _consumerTask ??= Task.Run(() => ConsumeAsync(_consumerCts.Token));

        _watchdogTimer ??= new Timer(WatchdogTick, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }

    public void Reinstall()
    {
        if (_hookThreadId == 0)
        {
            Start();
            return;
        }

        if (!PostThreadMessageW(_hookThreadId, WmAppReinstallHook, 0, 0))
        {
            _logger.LogWarning("PostThreadMessage(reinstall) failed with {Error}; restarting hook thread", Marshal.GetLastWin32Error());
            RestartHookThread();
        }
    }

    private void HookThreadProc(object? state)
    {
        var ready = (ManualResetEventSlim)state!;
        _current = this;

        var moduleHandle = GetModuleHandleW(null);
        // LibraryImport can't marshal delegates: pass a function pointer. HookProcDelegate is a
        // static field, so it is never collected while the hook is installed.
        _hookHandle = SetWindowsHookExW(WhKeyboardLl, Marshal.GetFunctionPointerForDelegate(HookProcDelegate), moduleHandle, 0);
        _hookThreadId = GetCurrentThreadId();
        ready.Set();

        if (_hookHandle == 0)
        {
            var error = Marshal.GetLastWin32Error();
            _logger.LogError("SetWindowsHookEx failed with {Error} — Typee will run without keyboard events", error);
            return;
        }

        Interlocked.Exchange(ref _lastSeenEventTick, Environment.TickCount);

        try
        {
            while (GetMessageW(out var msg, 0, 0, 0) > 0)
            {
                if (msg.Message == WmAppReinstallHook)
                {
                    ReinstallHookInternal();
                    continue;
                }

                _ = TranslateMessage(msg);
                _ = DispatchMessageW(msg);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keyboard hook message loop crashed");
        }
        finally
        {
            if (_hookHandle != 0)
            {
                _ = UnhookWindowsHookEx(_hookHandle);
                _hookHandle = 0;
            }

            _logger.LogInformation("Keyboard hook thread exited");
            if (!_disposing)
            {
                HookStoppedUnexpectedly?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void ReinstallHookInternal()
    {
        if (_hookHandle != 0)
        {
            _ = UnhookWindowsHookEx(_hookHandle);
            _hookHandle = 0;
        }

        var moduleHandle = GetModuleHandleW(null);
        _hookHandle = SetWindowsHookExW(WhKeyboardLl, Marshal.GetFunctionPointerForDelegate(HookProcDelegate), moduleHandle, 0);
        if (_hookHandle == 0)
        {
            _logger.LogError("Reinstall SetWindowsHookEx failed with {Error} — quitting hook thread for supervisor recovery",
                Marshal.GetLastWin32Error());
            // Exit the message loop so HookStoppedUnexpectedly fires and ServiceSupervisor can restart.
            PostQuitMessage(0);
        }
        else
        {
            _logger.LogWarning("Keyboard hook reinstalled");
            Interlocked.Exchange(ref _lastSeenEventTick, Environment.TickCount);
            Interlocked.Exchange(ref _lastReinstallTick, Environment.TickCount);
        }
    }

    private void RestartHookThread()
    {
        lock (_gate)
        {
            if (_thread is { IsAlive: true } && _hookThreadId != 0)
            {
                _ = PostThreadMessageW(_hookThreadId, WmQuit, 0, 0);
                _thread.Join(TimeSpan.FromSeconds(2));
            }

            _thread = null;
            _hookHandle = 0;
            _hookThreadId = 0;
        }

        Start();
    }

    private void WatchdogTick(object? _)
    {
        if (_disposing)
        {
            return;
        }

        // If the hook vanished but the thread is still "alive", force a supervised restart.
        if (_hookHandle == 0)
        {
            if (_thread is { IsAlive: true })
            {
                _logger.LogWarning("Hook watchdog found zero handle on live thread — restarting");
                RestartHookThread();
            }

            return;
        }

        // Probe: if the hook thread cannot accept messages, restart it.
        if (_hookThreadId != 0 && !PostThreadMessageW(_hookThreadId, WmNull, 0, 0))
        {
            _logger.LogWarning("Hook watchdog PostThreadMessage failed with {Error} — restarting", Marshal.GetLastWin32Error());
            RestartHookThread();
            return;
        }

        // Raw-keyboard sink (RIDEV_INPUTSINK) vs LL-hook timestamps — never key codes.
        // Skip when LL hook cannot see input (elevated target, secure desktop, lock screen).
        var now = Environment.TickCount;
        var lastRaw = _rawKeyboardClock.LastRawKeyboardTick;
        var lastHook = Volatile.Read(ref _lastSeenEventTick);
        var lastReinstall = Volatile.Read(ref _lastReinstallTick);
        var elevated = ProcessElevation.IsForegroundElevatedOrUnknown();
        var secureDesktop = SecureDesktopProbe.IsSecureDesktopActive();
        var locked = _sessionLockState.IsLocked;
        if (HookWatchdogDecision.ShouldReinstall(
                now,
                lastRaw,
                lastHook,
                lastReinstall,
                foregroundElevatedOrUnknown: elevated,
                secureDesktopActive: secureDesktop,
                sessionLocked: locked))
        {
            _logger.LogWarning("Hook watchdog: raw keyboard recent but hook quiet — reinstalling");
            Reinstall();
        }
    }

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var keyboardEvent in _channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var handlers = KeyEvent;
                if (handlers is null)
                {
                    continue;
                }

                // Invoke each subscriber separately: one failing consumer must not stop the
                // loop or starve the others (fail safely — rule 5). Log the type only, never keys.
                foreach (var handler in handlers.GetInvocationList())
                {
                    try
                    {
                        ((EventHandler<KeyboardEvent>)handler)(this, keyboardEvent);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Keyboard event subscriber {Subscriber} threw", handler.Method.DeclaringType?.Name);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutdown.
        }
    }

    public void Dispose()
    {
        _disposing = true;
        _watchdogTimer?.Dispose();
        _watchdogTimer = null;

        lock (_gate)
        {
            if (_thread is { IsAlive: true } && _hookThreadId != 0)
            {
                _ = PostThreadMessageW(_hookThreadId, WmQuit, 0, 0);
                _thread.Join(TimeSpan.FromSeconds(2));
            }

            _thread = null;
            _current = null;
        }

        if (_consumerCts is not null)
        {
            _consumerCts.Cancel();
            _consumerCts.Dispose();
            _consumerCts = null;
        }

        _consumerTask?.Wait(TimeSpan.FromSeconds(2));
        _consumerTask = null;

        if (Interlocked.Read(ref _droppedEvents) > 0)
        {
            _logger.LogWarning("Keyboard event channel dropped {Count} events while overloaded", _droppedEvents);
        }
    }
}
