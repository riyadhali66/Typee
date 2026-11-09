using Typee.Core.Abstractions;
using Typee.Core.Settings;

namespace Typee.Core.Llm;

/// <summary>
/// Sliding per-minute + optional daily cap per profile. Thread-safe; never logs content.
/// </summary>
public sealed class LlmRateGuard : ILlmRateGuard
{
    private readonly ISettingsStore _settings;
    private readonly TimeProvider _time;
    private readonly object _gate = new();
    private readonly Dictionary<string, ProfileCounters> _profiles = new(StringComparer.OrdinalIgnoreCase);

    public LlmRateGuard(ISettingsStore settings, TimeProvider? time = null)
    {
        _settings = settings;
        _time = time ?? TimeProvider.System;
    }

    public event EventHandler<LlmRateGuardPausedEventArgs>? Paused;

    public bool IsPaused(string profileName)
    {
        lock (_gate)
        {
            return GetOrCreate(profileName).Paused;
        }
    }

    public string? PauseReason(string profileName)
    {
        lock (_gate)
        {
            var counters = GetOrCreate(profileName);
            return counters.Paused ? counters.PauseReason : null;
        }
    }

    public bool TryAcquire(string profileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileName);
        var settings = _settings.Current.Llm;
        var maxPerMinute = Math.Max(1, settings.MaxRequestsPerMinute);
        var dailyCap = settings.DailyRequestCap;

        LlmRateGuardPausedEventArgs? pausedArgs = null;
        bool acquired;

        lock (_gate)
        {
            var counters = GetOrCreate(profileName);
            var now = _time.GetUtcNow();
            PruneMinuteWindow(counters, now);

            if (counters.Paused)
            {
                return false;
            }

            if (dailyCap is > 0)
            {
                EnsureDayBucket(counters, now);
                if (counters.DayCount >= dailyCap.Value)
                {
                    counters.Paused = true;
                    counters.PauseReason = "daily request cap reached";
                    pausedArgs = new LlmRateGuardPausedEventArgs
                    {
                        ProfileName = profileName,
                        Reason = counters.PauseReason
                    };
                    acquired = false;
                }
                else if (counters.MinuteHits.Count >= maxPerMinute)
                {
                    counters.Paused = true;
                    counters.PauseReason = "per-minute request limit reached";
                    pausedArgs = new LlmRateGuardPausedEventArgs
                    {
                        ProfileName = profileName,
                        Reason = counters.PauseReason
                    };
                    acquired = false;
                }
                else
                {
                    counters.MinuteHits.Add(now);
                    counters.DayCount++;
                    acquired = true;
                }
            }
            else if (counters.MinuteHits.Count >= maxPerMinute)
            {
                counters.Paused = true;
                counters.PauseReason = "per-minute request limit reached";
                pausedArgs = new LlmRateGuardPausedEventArgs
                {
                    ProfileName = profileName,
                    Reason = counters.PauseReason
                };
                acquired = false;
            }
            else
            {
                counters.MinuteHits.Add(now);
                acquired = true;
            }
        }

        if (pausedArgs is not null)
        {
            Paused?.Invoke(this, pausedArgs);
        }

        return acquired;
    }

    /// <summary>Test helper: clear pause so subsequent acquires can proceed after limits change.</summary>
    public void ResetPause(string profileName)
    {
        lock (_gate)
        {
            if (_profiles.TryGetValue(profileName, out var counters))
            {
                counters.Paused = false;
                counters.PauseReason = null;
            }
        }
    }

    private ProfileCounters GetOrCreate(string profileName)
    {
        if (!_profiles.TryGetValue(profileName, out var counters))
        {
            counters = new ProfileCounters();
            _profiles[profileName] = counters;
        }

        return counters;
    }

    private static void PruneMinuteWindow(ProfileCounters counters, DateTimeOffset now)
    {
        var cutoff = now - TimeSpan.FromMinutes(1);
        while (counters.MinuteHits.Count > 0 && counters.MinuteHits[0] < cutoff)
        {
            counters.MinuteHits.RemoveAt(0);
        }

        // Auto-clear per-minute pause once the window has capacity again.
        if (counters.Paused
            && string.Equals(counters.PauseReason, "per-minute request limit reached", StringComparison.Ordinal)
            && counters.MinuteHits.Count == 0)
        {
            counters.Paused = false;
            counters.PauseReason = null;
        }
    }

    private static void EnsureDayBucket(ProfileCounters counters, DateTimeOffset now)
    {
        var day = DateOnly.FromDateTime(now.UtcDateTime);
        if (counters.Day != day)
        {
            counters.Day = day;
            counters.DayCount = 0;
            if (counters.Paused
                && string.Equals(counters.PauseReason, "daily request cap reached", StringComparison.Ordinal))
            {
                counters.Paused = false;
                counters.PauseReason = null;
            }
        }
    }

    private sealed class ProfileCounters
    {
        public List<DateTimeOffset> MinuteHits { get; } = [];
        public DateOnly Day { get; set; }
        public int DayCount { get; set; }
        public bool Paused { get; set; }
        public string? PauseReason { get; set; }
    }
}
