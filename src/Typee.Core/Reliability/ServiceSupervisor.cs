using Microsoft.Extensions.Logging;

namespace Typee.Core.Reliability;

/// <summary>
/// Supervises a named background feature with exponential-ish backoff restarts.
/// Gives up after five failures within ten minutes and raises <see cref="GiveUp"/>.
/// </summary>
public sealed class ServiceSupervisor : IDisposable
{
    private static readonly TimeSpan[] BackoffSteps =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30)
    ];

    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(10);
    private const int MaxFailuresInWindow = 5;

    private readonly string _featureName;
    private readonly Action _start;
    private readonly Action? _stop;
    private readonly ILogger _logger;
    private readonly object _gate = new();
    private readonly List<DateTimeOffset> _failureTimes = [];
    private int _backoffIndex;
    private CancellationTokenSource? _pendingRestart;
    private bool _disposed;

    public ServiceSupervisor(string featureName, Action start, Action? stop, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(featureName);
        _featureName = featureName;
        _start = start ?? throw new ArgumentNullException(nameof(start));
        _stop = stop;
        _logger = logger;
    }

    public event EventHandler<SupervisorEventArgs>? GiveUp;

    public event EventHandler<SupervisorEventArgs>? Restarting;

    public void StartInitial() => _start();

    public void NotifyStoppedUnexpectedly(string reason)
    {
        if (_disposed)
        {
            return;
        }

        lock (_gate)
        {
            var now = DateTimeOffset.UtcNow;
            _failureTimes.RemoveAll(t => now - t > FailureWindow);
            _failureTimes.Add(now);

            if (_failureTimes.Count >= MaxFailuresInWindow)
            {
                _logger.LogError(
                    "Supervisor giving up on {Feature} after {Count} failures within {WindowMinutes} minutes ({Reason})",
                    _featureName,
                    _failureTimes.Count,
                    FailureWindow.TotalMinutes,
                    reason);
                GiveUp?.Invoke(this, new SupervisorEventArgs(_featureName, reason));
                return;
            }

            var delay = BackoffSteps[Math.Min(_backoffIndex, BackoffSteps.Length - 1)];
            _backoffIndex++;

            _logger.LogWarning(
                "Supervisor scheduling restart of {Feature} in {DelayMs} ms ({Reason})",
                _featureName,
                (int)delay.TotalMilliseconds,
                reason);

            Restarting?.Invoke(this, new SupervisorEventArgs(_featureName, reason));

            _pendingRestart?.Cancel();
            _pendingRestart = new CancellationTokenSource();
            var token = _pendingRestart.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(delay, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested || _disposed)
                    {
                        return;
                    }

                    _stop?.Invoke();
                    _start();
                    _logger.LogInformation("Supervisor restarted {Feature}", _featureName);
                }
                catch (OperationCanceledException)
                {
                    // Superseded by a newer restart request.
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Supervisor restart of {Feature} failed", _featureName);
                }
            }, token);
        }
    }

    public void NotifyHealthy()
    {
        lock (_gate)
        {
            _backoffIndex = 0;
            _failureTimes.Clear();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pendingRestart?.Cancel();
        _pendingRestart?.Dispose();
        _pendingRestart = null;
    }
}

public sealed class SupervisorEventArgs(string featureName, string reason) : EventArgs
{
    public string FeatureName { get; } = featureName;
    public string Reason { get; } = reason;
}
