using Microsoft.Extensions.Time.Testing;
using Typee.Core.Abstractions;
using Typee.Core.Input;
using Typee.Core.Typing;

namespace Typee.Core.Tests;

public sealed class TypingActivityServiceTests : IDisposable
{
    private readonly FakeKeyboardEvents _keyboard = new();
    private readonly FakeTimeProvider _time = new();
    private readonly TypingActivityService _service;
    private readonly List<string> _events = [];

    public TypingActivityServiceTests()
    {
        _service = new TypingActivityService(_keyboard, _time, debounceMs: 500);
        _service.TypingPaused += (_, _) => _events.Add("paused");
        _service.TypingResumed += (_, _) => _events.Add("resumed");
    }

    [Fact]
    public void SingleKey_RaisesResumedThenPausedAfterDebounce()
    {
        Press('A');

        Assert.Equal(["resumed"], _events);

        _time.Advance(TimeSpan.FromMilliseconds(499));
        Assert.Equal(["resumed"], _events);

        _time.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(["resumed", "paused"], _events);
    }

    [Fact]
    public void ContinuedTyping_RearmsDebounce_NoIntermediatePause()
    {
        Press('A');
        _time.Advance(TimeSpan.FromMilliseconds(400));
        Press('B');
        _time.Advance(TimeSpan.FromMilliseconds(400));
        Press('C');

        // 800 ms since the first key, but each press re-arms the timer.
        Assert.Equal(["resumed"], _events);

        _time.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(["resumed", "paused"], _events);
    }

    [Fact]
    public void KeyUpsDoNotCount_AsTyping()
    {
        Press('A');
        _time.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(["resumed", "paused"], _events);

        _keyboard.Raise(new KeyboardEvent('A', IsKeyDown: false, Timestamp: 1));
        Assert.Equal(["resumed", "paused"], _events);
    }

    [Fact]
    public void AfterPause_NextKeyRaisesResumedAgain()
    {
        Press('A');
        _time.Advance(TimeSpan.FromMilliseconds(500));
        Press('B');

        Assert.Equal(["resumed", "paused", "resumed"], _events);
    }

    [Fact]
    public void Configure_NewDebounceAppliesToPendingTimer()
    {
        Press('A');
        _service.Configure(1000);
        _time.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(["resumed"], _events);

        _time.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(["resumed", "paused"], _events);
    }

    [Fact]
    public void StaleTimerFire_WhilePaused_IsIgnored()
    {
        Press('A');
        _time.Advance(TimeSpan.FromMilliseconds(500));
        _time.Advance(TimeSpan.FromMilliseconds(5000));
        Assert.Equal(["resumed", "paused"], _events);
    }

    private void Press(char vk) => _keyboard.Raise(new KeyboardEvent(vk, IsKeyDown: true, Timestamp: 0));

    public void Dispose() => _service.Dispose();

    private sealed class FakeKeyboardEvents : IKeyboardEvents
    {
        public event EventHandler<KeyboardEvent>? KeyEvent;

        public void Raise(KeyboardEvent keyboardEvent) => KeyEvent?.Invoke(this, keyboardEvent);
    }
}
