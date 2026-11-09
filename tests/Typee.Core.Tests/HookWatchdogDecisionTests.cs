using Typee.Core.Typing;

namespace Typee.Core.Tests;

public sealed class HookWatchdogDecisionTests
{
    [Fact]
    public void ShouldReinstall_WhenRawRecentAndHookStale_AndRateLimitElapsed()
    {
        const int now = 100_000;
        var lastRaw = now - 1_000;      // raw saw keyboard 1s ago
        var lastHook = now - 6_000;     // hook quiet for 6s
        var lastReinstall = now - 31_000;

        Assert.True(HookWatchdogDecision.ShouldReinstall(now, lastRaw, lastHook, lastReinstall));
    }

    [Fact]
    public void ShouldNotReinstall_WhenRawAlsoStale()
    {
        const int now = 100_000;
        Assert.False(HookWatchdogDecision.ShouldReinstall(
            now,
            lastRawKeyboardTick: now - 6_000,
            lastHookTick: now - 6_000,
            lastReinstallTick: now - 31_000));
    }

    [Fact]
    public void ShouldNotReinstall_WhenHookAlsoRecent()
    {
        const int now = 100_000;
        Assert.False(HookWatchdogDecision.ShouldReinstall(
            now,
            lastRawKeyboardTick: now - 1_000,
            lastHookTick: now - 500,
            lastReinstallTick: now - 31_000));
    }

    [Fact]
    public void ShouldNotReinstall_WhenRateLimited()
    {
        const int now = 100_000;
        Assert.False(HookWatchdogDecision.ShouldReinstall(
            now,
            lastRawKeyboardTick: now - 1_000,
            lastHookTick: now - 6_000,
            lastReinstallTick: now - 10_000));
    }

    [Fact]
    public void ShouldReinstall_HandlesTickCountWrapAround()
    {
        // Simulate wrap: now is small, past ticks are near int.MaxValue.
        const int now = 2_000;
        var lastRaw = unchecked(now - 1_000);       // wraps
        var lastHook = unchecked(now - 6_000);
        var lastReinstall = unchecked(now - 31_000);

        Assert.True(HookWatchdogDecision.ShouldReinstall(now, lastRaw, lastHook, lastReinstall));
    }

    [Fact]
    public void ShouldNotReinstall_WhenForegroundElevated()
    {
        const int now = 100_000;
        Assert.False(HookWatchdogDecision.ShouldReinstall(
            now,
            lastRawKeyboardTick: now - 1_000,
            lastHookTick: now - 6_000,
            lastReinstallTick: now - 31_000,
            foregroundElevatedOrUnknown: true));
    }

    [Fact]
    public void ShouldNotReinstall_WhenSecureDesktopActive()
    {
        const int now = 100_000;
        Assert.False(HookWatchdogDecision.ShouldReinstall(
            now,
            lastRawKeyboardTick: now - 1_000,
            lastHookTick: now - 6_000,
            lastReinstallTick: now - 31_000,
            secureDesktopActive: true));
    }

    [Fact]
    public void ShouldNotReinstall_WhenSessionLocked()
    {
        const int now = 100_000;
        Assert.False(HookWatchdogDecision.ShouldReinstall(
            now,
            lastRawKeyboardTick: now - 1_000,
            lastHookTick: now - 6_000,
            lastReinstallTick: now - 31_000,
            sessionLocked: true));
    }
}
