using Typee.Core.Abstractions;
using Typee.Core.Input;

namespace Typee.Core.Typing;

/// <summary>
/// Raises <see cref="TypingResumed"/> on the first key press after a pause and
/// <see cref="TypingPaused"/> once no key has been pressed for the debounce interval.
/// Events fire on a timer/thread-pool thread — handlers must marshal.
/// </summary>
public sealed class TypingActivityService : IDisposable
{
    private readonly object _gate = new();
    private readonly ITimer _timer;
    private readonly IKeyboardEvents _keyboardEvents;
    private int _debounceMs;
    private bool _isPaused = true;

    public TypingActivityService(IKeyboardEvents keyboardEvents, TimeProvider timeProvider, int debounceMs)
    {
        _debounceMs = Math.Max(50, debounceMs);
        _timer = timeProvider.CreateTimer(OnTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _keyboardEvents = keyboardEvents;
        _keyboardEvents.KeyEvent += OnKeyEvent;
    }

    public event EventHandler? TypingPaused;
    public event EventHandler? TypingResumed;

    /// <summary>Updates the debounce interval (settings hot-reload).</summary>
    public void Configure(int debounceMs)
    {
        lock (_gate)
        {
            _debounceMs = Math.Max(50, debounceMs);
            if (!_isPaused)
            {
                // Re-arm with the new interval from now.
                _timer.Change(TimeSpan.FromMilliseconds(_debounceMs), Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void OnKeyEvent(object? sender, KeyboardEvent e)
    {
        if (!e.IsKeyDown)
        {
            return;
        }

        bool resumed;
        lock (_gate)
        {
            _timer.Change(TimeSpan.FromMilliseconds(_debounceMs), Timeout.InfiniteTimeSpan);
            resumed = _isPaused;
            _isPaused = false;
        }

        // Raise outside the lock so a handler that calls back in (e.g. Configure) can't deadlock.
        if (resumed)
        {
            TypingResumed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnTimer(object? state)
    {
        lock (_gate)
        {
            if (_isPaused)
            {
                return;
            }

            _isPaused = true;
        }

        TypingPaused?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _keyboardEvents.KeyEvent -= OnKeyEvent;
        _timer.Dispose();
    }
}
