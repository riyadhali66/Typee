using System.Threading.Channels;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Input;
using Typee.Core.Typing;
using Typee.Platform.Windows.Interop;
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
    private readonly ILogger<KeyboardHookService> _logger;
    private readonly Channel<KeyboardEvent> _channel = Channel.CreateBounded<KeyboardEvent>(
        new BoundedChannelOptions(ChannelCapacity) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite });

    private readonly object _gate = new();
    private Thread? _thread;
    private nint _hookHandle;
    private uint _hookThreadId;
    private long _droppedEvents;
    private CancellationTokenSource? _consumerCts;
    private Task? _consumerTask;

    private static readonly HookProc HookProcDelegate = HookProcCallback;

    private static unsafe nint HookProcCallback(int code, nint wParam, nint lParam)
    {
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

        if (_current is not null && !_current._channel.Writer.TryWrite(new KeyboardEvent(data.VkCode, isKeyDown, (uint)data.Time)))
        {
            Interlocked.Increment(ref _current._droppedEvents);
        }

        return CallNextHookEx(0, code, wParam, lParam);
    }

    private static KeyboardHookService? _current;

    public KeyboardHookService(KeyboardInterceptionState interception, ILogger<KeyboardHookService> logger)
    {
        _interception = interception;
        _logger = logger;
    }

    public event EventHandler<KeyboardEvent>? KeyEvent;

    /// <summary>Installs the hook. Safe to call once; subsequent calls are no-ops.</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_thread is { IsAlive: true })
            {
                return;
            }

            var ready = new ManualResetEventSlim(false);
            _thread = new Thread(HookThreadProc) { Name = "Typee.KeyboardHook", IsBackground = true };
            _thread.Start(ready);
            ready.Wait(TimeSpan.FromSeconds(5));
        }

        _consumerCts = new CancellationTokenSource();
        _consumerTask = Task.Run(() => ConsumeAsync(_consumerCts.Token));
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

        try
        {
            while (GetMessageW(out var msg, 0, 0, 0) > 0)
            {
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
