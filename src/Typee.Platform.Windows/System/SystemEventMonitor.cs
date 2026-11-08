using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Platform.Windows.Interop;
using Typee.Platform.Windows.Keyboard;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.SystemEvents;

/// <summary>
/// Observes session unlock, power resume, Explorer taskbar recreation, and raw keyboard
/// activity (timestamp only) for the hook watchdog.
/// </summary>
public sealed class SystemEventMonitor : IDisposable
{
    private readonly ILogger<SystemEventMonitor> _logger;
    private readonly IRawKeyboardActivityClock _rawKeyboardClock;
    private readonly MessageWindow _window;
    private readonly uint _taskbarCreatedMessage;
    private bool _disposed;

    public SystemEventMonitor(
        ILogger<SystemEventMonitor> logger,
        IRawKeyboardActivityClock rawKeyboardClock)
    {
        _logger = logger;
        _rawKeyboardClock = rawKeyboardClock;
        _window = new MessageWindow(logger);
        _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");
        _window.SetCustomMessageHandler(HandleMessage);

        if (_window.Handle == 0)
        {
            _logger.LogError("System event monitor could not create message window");
            return;
        }

        if (!WTSRegisterSessionNotification(_window.Handle, NotifyForThisSession))
        {
            _logger.LogWarning("WTSRegisterSessionNotification failed with {Error}", Marshal.GetLastWin32Error());
        }
        else
        {
            _logger.LogInformation("Registered for WTS session notifications");
        }

        RegisterKeyboardRawInput(_window.Handle);
    }

    public event EventHandler? SystemResumed;
    public event EventHandler? TaskbarCreated;

    private void RegisterKeyboardRawInput(nint hwnd)
    {
        var device = new RawInputDevice
        {
            UsUsagePage = HidUsagePageGeneric,
            UsUsage = HidUsageGenericKeyboard,
            DwFlags = RidevInputSink,
            HwndTarget = hwnd
        };

        if (!RegisterRawInputDevices(in device, 1, (uint)Marshal.SizeOf<RawInputDevice>()))
        {
            _logger.LogWarning("RegisterRawInputDevices(keyboard) failed with {Error}", Marshal.GetLastWin32Error());
            return;
        }

        _logger.LogInformation("Registered raw keyboard input sink for hook watchdog (timestamps only)");
    }

    private bool HandleMessage(uint msg, nint wParam, nint lParam)
    {
        if (msg == WmInput)
        {
            NoteRawKeyboardIfPresent(lParam);
            return true;
        }

        if (msg == WmWtsSessionChange)
        {
            var sessionEvent = (int)wParam;
            if (sessionEvent is WtsSessionUnlock or WtsSessionLogon)
            {
                _logger.LogInformation("WTS session change {Event} — raising SystemResumed", sessionEvent);
                SystemResumed?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }

        if (msg == WmPowerBroadcast)
        {
            var powerEvent = (int)wParam;
            if (powerEvent is PbtApmResumeAutomatic or PbtApmResumeSuspend)
            {
                _logger.LogInformation("Power broadcast {Event} — raising SystemResumed", powerEvent);
                SystemResumed?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }

        if (msg == _taskbarCreatedMessage)
        {
            _logger.LogInformation("TaskbarCreated — shell taskbar was recreated");
            TaskbarCreated?.Invoke(this, EventArgs.Empty);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Reads only <see cref="RawInputHeader"/> (RID_HEADER). Never reads VKey / MakeCode.
    /// </summary>
    private void NoteRawKeyboardIfPresent(nint hRawInput)
    {
        var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        var size = headerSize;
        var buffer = Marshal.AllocHGlobal((int)headerSize);
        try
        {
            var written = GetRawInputData(hRawInput, RidHeader, buffer, ref size, headerSize);
            if (written == unchecked((uint)-1) || written < headerSize)
            {
                return;
            }

            var header = Marshal.PtrToStructure<RawInputHeader>(buffer);
            if (header.DwType == RimTypeKeyboard)
            {
                _rawKeyboardClock.NoteKeyboardActivity();
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_window.Handle != 0)
        {
            _ = WTSUnRegisterSessionNotification(_window.Handle);
        }

        _window.Dispose();
    }
}
