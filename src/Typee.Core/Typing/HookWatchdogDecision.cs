namespace Typee.Core.Typing;

/// <summary>
/// Pure decision logic for the LL-hook watchdog. Compares raw-keyboard activity
/// timestamps to hook activity — never involves key codes or text.
/// </summary>
public static class HookWatchdogDecision
{
    public const int RawRecentWindowMs = 5_000;
    public const int MinReinstallIntervalMs = 30_000;

    /// <summary>
    /// Returns true when raw input saw a keyboard event in the last <see cref="RawRecentWindowMs"/>
    /// but the hook did not, and the reinstall rate limit has elapsed.
    /// Tick values use <see cref="Environment.TickCount"/> wrap-around arithmetic.
    /// </summary>
    public static bool ShouldReinstall(
        int nowTick,
        int lastRawKeyboardTick,
        int lastHookTick,
        int lastReinstallTick,
        int rawRecentWindowMs = RawRecentWindowMs,
        int minReinstallIntervalMs = MinReinstallIntervalMs)
    {
        var sinceReinstall = unchecked(nowTick - lastReinstallTick);
        if (sinceReinstall < minReinstallIntervalMs)
        {
            return false;
        }

        var rawAge = unchecked(nowTick - lastRawKeyboardTick);
        if (rawAge < 0 || rawAge > rawRecentWindowMs)
        {
            return false;
        }

        // Hook missed that recent raw keyboard activity (hook quieter than the raw window).
        var hookAge = unchecked(nowTick - lastHookTick);
        return hookAge > rawRecentWindowMs;
    }
}
