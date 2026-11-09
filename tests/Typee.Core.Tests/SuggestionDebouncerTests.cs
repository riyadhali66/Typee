using Microsoft.Extensions.Time.Testing;
using Typee.Core.Abstractions;
using Typee.Core.Input;
using Typee.Core.Typing;

namespace Typee.Core.Tests;

/// <summary>
/// Debounce coverage for the typing-pause gate that drives the Phase 7 pipeline.
/// </summary>
public sealed class SuggestionDebouncerTests
{
    [Fact]
    public void TypingPaused_FiresAfterDebounce_NotBefore()
    {
        var time = new FakeTimeProvider();
        var keyboard = new FakeKeyboard();
        using var activity = new TypingActivityService(keyboard, time, debounceMs: 500);
        var paused = 0;
        activity.TypingPaused += (_, _) => paused++;

        keyboard.Raise(new KeyboardEvent(0x41, IsKeyDown: true, Flags: 0, Modifiers: KeyModifiers.None, Timestamp: 1));
        Assert.Equal(0, paused);

        time.Advance(TimeSpan.FromMilliseconds(499));
        Assert.Equal(0, paused);

        time.Advance(TimeSpan.FromMilliseconds(2));
        Assert.Equal(1, paused);
    }

    [Fact]
    public void TypingResumed_RearmsDebounce()
    {
        var time = new FakeTimeProvider();
        var keyboard = new FakeKeyboard();
        using var activity = new TypingActivityService(keyboard, time, debounceMs: 500);
        var paused = 0;
        var resumed = 0;
        activity.TypingPaused += (_, _) => paused++;
        activity.TypingResumed += (_, _) => resumed++;

        keyboard.Raise(new KeyboardEvent(0x41, true, 0, KeyModifiers.None, 1));
        time.Advance(TimeSpan.FromMilliseconds(200));
        keyboard.Raise(new KeyboardEvent(0x42, true, 0, KeyModifiers.None, 2));
        Assert.Equal(1, resumed);

        time.Advance(TimeSpan.FromMilliseconds(500));
        Assert.Equal(1, paused);
    }

    private sealed class FakeKeyboard : IKeyboardEvents
    {
        public event EventHandler<KeyboardEvent>? KeyEvent;
        public void Raise(KeyboardEvent e) => KeyEvent?.Invoke(this, e);
    }
}
