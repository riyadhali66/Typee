using Microsoft.Extensions.Time.Testing;
using Typee.Core.Abstractions;
using Typee.Core.Llm;
using Typee.Core.Settings;

namespace Typee.Core.Tests;

public sealed class LlmRateGuardTests
{
    [Fact]
    public void TryAcquire_RespectsPerMinuteLimit_AndPauses()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var settings = new FakeSettingsStore(new AppSettings
        {
            Llm = { MaxRequestsPerMinute = 3, DailyRequestCap = null }
        });
        var guard = new LlmRateGuard(settings, time);
        LlmRateGuardPausedEventArgs? paused = null;
        guard.Paused += (_, e) => paused = e;

        Assert.True(guard.TryAcquire("default"));
        Assert.True(guard.TryAcquire("default"));
        Assert.True(guard.TryAcquire("default"));
        Assert.False(guard.TryAcquire("default"));
        Assert.True(guard.IsPaused("default"));
        Assert.NotNull(paused);
        Assert.Contains("per-minute", paused!.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryAcquire_RespectsDailyCap()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var settings = new FakeSettingsStore(new AppSettings
        {
            Llm = { MaxRequestsPerMinute = 100, DailyRequestCap = 2 }
        });
        var guard = new LlmRateGuard(settings, time);

        Assert.True(guard.TryAcquire("default"));
        Assert.True(guard.TryAcquire("default"));
        Assert.False(guard.TryAcquire("default"));
        Assert.Equal("daily request cap reached", guard.PauseReason("default"));
    }

    private sealed class FakeSettingsStore : ISettingsStore
    {
        public FakeSettingsStore(AppSettings current) => Current = current;
        public string SettingsPath => "test-settings.json";
        public AppSettings Current { get; private set; }
        public IReadOnlyList<string> LastLoadNotices { get; } = [];
        public event EventHandler<AppSettings>? SettingsChanged;
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Current = settings;
            SettingsChanged?.Invoke(this, settings);
            return Task.CompletedTask;
        }
        public Task ExportAsync(string destinationPath, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ImportAsync(string sourcePath, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResetToDefaultsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
