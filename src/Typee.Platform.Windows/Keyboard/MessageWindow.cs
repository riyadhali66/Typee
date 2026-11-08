using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Typee.Platform.Windows.Interop;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.Keyboard;

/// <summary>
/// A message-only window (HWND_MESSAGE) on a dedicated thread with a real GetMessage loop.
/// Used for RegisterHotKey and for system-event monitoring. Multiple instances are supported;
/// WndProc routes by HWND (never a process-wide singleton).
/// </summary>
internal sealed class MessageWindow : IDisposable
{
    private static readonly ConcurrentDictionary<nint, MessageWindow> WindowsByHwnd = new();
    private static readonly WndProc WndProcDelegate = WndProcCallback;

    private readonly ILogger _logger;
    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly object _gate = new();
    private readonly string _className = $"TypeeMsgWnd_{Environment.ProcessId}_{Guid.NewGuid():N}";
    private readonly ManualResetEventSlim _ready = new(false);
    private Thread? _thread;
    private nint _hwnd;
    private bool _disposed;
    private Action<int>? _hotkeyHandlers;
    private Func<uint, nint, nint, bool>? _customMessageHandler;

    public MessageWindow(ILogger logger)
    {
        _logger = logger;
        _thread = new Thread(WindowThreadProc) { Name = "Typee.MessageWindow", IsBackground = true };
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(5));
    }

    /// <summary>The message-only window handle (zero if creation failed).</summary>
    public nint Handle => _hwnd;

    /// <summary>Runs the action on the window thread. Blocks up to <paramref name="timeout"/>.</summary>
    public bool TryRun(Action action, TimeSpan timeout)
    {
        if (_hwnd == 0)
        {
            return false;
        }

        var done = new ManualResetEventSlim(false);
        Exception? failure = null;
        _queue.Enqueue(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                // Never let an exception unwind into the native WndProc — capture it and
                // rethrow on the calling thread instead.
                failure = ex;
            }
            finally
            {
                done.Set();
            }
        });

        if (!PostMessageW(_hwnd, WmAppRunQueue, 0, 0))
        {
            _logger.LogWarning("PostMessage(WM_APP_RUN) failed with {Error}", Marshal.GetLastWin32Error());
            return false;
        }

        if (!done.Wait(timeout))
        {
            return false;
        }

        if (failure is not null)
        {
            global::System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(failure);
        }

        return true;
    }

    private void WindowThreadProc()
    {
        var hInstance = GetModuleHandleW(null);
        var classNamePtr = Marshal.StringToHGlobalUni(_className);
        try
        {
            var wc = new WndClassEx
            {
                CbSize = (uint)Marshal.SizeOf<WndClassEx>(),
                LpfnWndProc = Marshal.GetFunctionPointerForDelegate(WndProcDelegate),
                HInstance = hInstance,
                LpszClassName = (nuint)classNamePtr
            };

            var atom = RegisterClassExW(wc);
            if (atom == 0)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 1410) // ERROR_CLASS_ALREADY_EXISTS
                {
                    _logger.LogError("RegisterClassEx failed with {Error}", error);
                    _ready.Set();
                    return;
                }
            }

            _hwnd = CreateWindowExW(
                0,
                (nuint)classNamePtr,
                (nuint)classNamePtr,
                0,
                0, 0, 0, 0,
                HwndMessage, 0, hInstance, 0);

            if (_hwnd == 0)
            {
                _logger.LogError("CreateWindowEx(message-only) failed with {Error}", Marshal.GetLastWin32Error());
            }
            else
            {
                WindowsByHwnd[_hwnd] = this;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(classNamePtr);
        }

        _ready.Set();

        if (_hwnd == 0)
        {
            return;
        }

        try
        {
            while (GetMessageW(out var msg, 0, 0, 0) > 0)
            {
                _ = DispatchMessageW(msg);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Message window loop crashed");
        }
        finally
        {
            WindowsByHwnd.TryRemove(_hwnd, out _);
            if (_hwnd != 0)
            {
                _ = DestroyWindow(_hwnd);
                _hwnd = 0;
            }
        }
    }

    private void DrainQueue()
    {
        while (_queue.TryDequeue(out var action))
        {
            action();
        }
    }

    private static nint WndProcCallback(nint hWnd, uint msg, nint wParam, nint lParam)
    {
        if (!WindowsByHwnd.TryGetValue(hWnd, out var window))
        {
            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        if (msg == WmAppRunQueue)
        {
            window.DrainQueue();
            return 0;
        }

        if (msg == WmHotkey)
        {
            window._hotkeyHandlers?.Invoke((int)wParam);
            return 0;
        }

        if (msg == WmClose)
        {
            _ = DestroyWindow(hWnd);
            return 0;
        }

        if (msg == WmDestroy)
        {
            PostQuitMessage(0);
            return 0;
        }

        if (window._customMessageHandler?.Invoke(msg, wParam, lParam) == true)
        {
            return 0;
        }

        return DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    /// <summary>Registers a handler invoked on the window thread with the hotkey id from WM_HOTKEY.</summary>
    public void SetHotkeyHandler(Action<int> handler) => _hotkeyHandlers = handler;

    public void SetCustomMessageHandler(Func<uint, nint, nint, bool>? handler) => _customMessageHandler = handler;

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        if (_hwnd != 0)
        {
            WindowsByHwnd.TryRemove(_hwnd, out _);
            _ = PostMessageW(_hwnd, WmClose, 0, 0);
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
        _ready.Dispose();
    }
}
