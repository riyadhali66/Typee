using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Interop.UIAutomationClient;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.Uia;

/// <summary>Dedicated STA thread for all UI Automation COM calls (150 ms default timeout).</summary>
public sealed class UiaThread : IDisposable
{
    public static readonly TimeSpan DefaultCallTimeout = TimeSpan.FromMilliseconds(150);

    private readonly ILogger<UiaThread> _logger;
    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly ManualResetEventSlim _ready = new(false);
    private readonly Thread _thread;
    private CUIAutomation8? _automation;
    private nint _hwnd;
    private bool _disposed;

    public UiaThread(ILogger<UiaThread> logger)
    {
        _logger = logger;
        _thread = new Thread(ThreadMain)
        {
            Name = "Typee.UiaThread",
            IsBackground = true
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        if (!_ready.Wait(TimeSpan.FromSeconds(5)))
        {
            _logger.LogError("UIA thread failed to become ready");
        }
    }

    public nint WindowHandle => _hwnd;

    public T Invoke<T>(Func<CUIAutomation8, T> action, TimeSpan? timeout = null)
    {
        if (_disposed || _hwnd == 0 || _automation is null)
        {
            return default!;
        }

        T? result = default;
        Exception? failure = null;
        var done = new ManualResetEventSlim(false);
        _queue.Enqueue(() =>
        {
            try
            {
                result = action(_automation);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            finally
            {
                done.Set();
            }
        });

        if (!PostMessageW(_hwnd, WmAppRunQueue, 0, 0))
        {
            _logger.LogWarning("PostMessage to UIA thread failed with {Error}", Marshal.GetLastWin32Error());
            return default!;
        }

        if (!done.Wait(timeout ?? DefaultCallTimeout))
        {
            _logger.LogDebug("UIA call timed out after {TimeoutMs} ms", (timeout ?? DefaultCallTimeout).TotalMilliseconds);
            return default!;
        }

        if (failure is not null)
        {
            _logger.LogDebug(failure, "UIA call failed");
            return default!;
        }

        return result!;
    }

    public void Invoke(Action<CUIAutomation8> action, TimeSpan? timeout = null) =>
        Invoke(automation =>
        {
            action(automation);
            return true;
        }, timeout);

    private void ThreadMain()
    {
        try
        {
            _automation = new CUIAutomation8();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create CUIAutomation8");
            _ready.Set();
            return;
        }

        var className = $"TypeeUiaPump_{Environment.ProcessId}_{Guid.NewGuid():N}";
        var classNamePtr = Marshal.StringToHGlobalUni(className);
        try
        {
            var hInstance = GetModuleHandleW(null);
            var wc = new WndClassEx
            {
                CbSize = (uint)Marshal.SizeOf<WndClassEx>(),
                LpfnWndProc = Marshal.GetFunctionPointerForDelegate(UiaWndProc.Default),
                HInstance = hInstance,
                LpszClassName = (nuint)classNamePtr
            };

            if (RegisterClassExW(wc) == 0)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 1410)
                {
                    _logger.LogError("RegisterClassEx for UIA pump failed with {Error}", error);
                    _ready.Set();
                    return;
                }
            }

            _hwnd = CreateWindowExW(0, (nuint)classNamePtr, (nuint)classNamePtr, 0, 0, 0, 0, 0, HwndMessage, 0, hInstance, 0);
            if (_hwnd == 0)
            {
                _logger.LogError("CreateWindowEx for UIA pump failed with {Error}", Marshal.GetLastWin32Error());
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

        UiaWndProc.Register(_hwnd, DrainQueue);

        try
        {
            while (GetMessageW(out var msg, 0, 0, 0) > 0)
            {
                _ = DispatchMessageW(msg);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UIA pump thread crashed");
        }
        finally
        {
            UiaWndProc.Unregister(_hwnd);
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
            try
            {
                action();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "UIA queued action failed");
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_hwnd != 0)
        {
            _ = PostMessageW(_hwnd, WmClose, 0, 0);
        }

        _thread.Join(TimeSpan.FromSeconds(2));
        _ready.Dispose();
    }

    private static class UiaWndProc
    {
        private static readonly WndProc Callback = WndProcImpl;
        private static readonly ConcurrentDictionary<nint, Action> Handlers = new();

        public static WndProc Default => Callback;

        public static void Register(nint hwnd, Action handler) => Handlers[hwnd] = handler;

        public static void Unregister(nint hwnd) => Handlers.TryRemove(hwnd, out _);

        private static nint WndProcImpl(nint hWnd, uint msg, nint wParam, nint lParam)
        {
            if (msg == WmAppRunQueue && Handlers.TryGetValue(hWnd, out var handler))
            {
                handler();
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

            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }
    }
}
