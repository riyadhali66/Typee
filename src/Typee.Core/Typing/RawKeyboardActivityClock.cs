using Typee.Core.Abstractions;

namespace Typee.Core.Typing;

/// <summary>Thread-safe tick clock for raw keyboard activity (timestamp only).</summary>
public sealed class RawKeyboardActivityClock : IRawKeyboardActivityClock
{
    private int _lastRawKeyboardTick = Environment.TickCount;

    public int LastRawKeyboardTick => Volatile.Read(ref _lastRawKeyboardTick);

    public void NoteKeyboardActivity() =>
        Interlocked.Exchange(ref _lastRawKeyboardTick, Environment.TickCount);
}
