namespace Typee.App.SingleInstance;

/// <summary>
/// Ensures only one Typee process owns the tray. A second launch signals the first to open Settings.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    public const string MutexName = @"Local\Typee.SingleInstance";
    public const string ActivateEventName = @"Local\Typee.ActivateSettings";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _listenerTask;
    private bool _ownsMutex;
    private bool _disposed;

    private SingleInstanceGuard(Mutex mutex, bool ownsMutex, EventWaitHandle activateEvent, Action onActivateRequested)
    {
        _mutex = mutex;
        _ownsMutex = ownsMutex;
        _activateEvent = activateEvent;
        _listenerTask = ownsMutex
            ? Task.Factory.StartNew(
                () => ListenForActivation(onActivateRequested, _cts.Token),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)
            : Task.CompletedTask;
    }

    public bool IsPrimaryInstance => _ownsMutex;

    public static SingleInstanceGuard Acquire(Action onActivateRequested)
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        return new SingleInstanceGuard(mutex, createdNew, activateEvent, onActivateRequested);
    }

    public void SignalActivateSettings()
    {
        _activateEvent.Set();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        try
        {
            _listenerTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Listener exit races are expected during shutdown.
        }

        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
            _ownsMutex = false;
        }

        _mutex.Dispose();
        _activateEvent.Dispose();
        _cts.Dispose();
    }

    private void ListenForActivation(Action onActivateRequested, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var signaled = WaitHandle.WaitAny([_activateEvent, cancellationToken.WaitHandle], 1000);
                if (signaled == 0)
                {
                    onActivateRequested();
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
