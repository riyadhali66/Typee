using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Typee.Platform.Windows.Interop;
using Typee.Platform.Windows.Keyboard;
using static Typee.Platform.Windows.Interop.NativeMethods;

namespace Typee.Platform.Windows.SystemEvents;

/// <summary>
/// Observes session unlock, power resume, and Explorer taskbar recreation.
/// </summary>
public sealed class SystemEventMonitor : IDisposable
{
    private readonly ILogger<SystemEventMonitor> _logger;
    private readonly MessageWindow _window;
    private readonly uint _taskbarCreatedMessage;
    private bool _disposed;

    public SystemEventMonitor(ILogger<SystemEventMonitor> logger)
    {
        _logger = logger;
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
    }

    public event EventHandler? SystemResumed;
    public event EventHandler? TaskbarCreated;

    private bool HandleMessage(uint msg, nint wParam, nint lParam)
    {
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
