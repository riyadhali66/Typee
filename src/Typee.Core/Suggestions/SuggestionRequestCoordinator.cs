namespace Typee.Core.Suggestions;

/// <summary>
/// Ensures only the latest suggestion request is live. Cancel on TypingResumed,
/// focus change, or a newer pause request.
/// </summary>
public sealed class SuggestionRequestCoordinator : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _current;
    private long _generation;
    private bool _disposed;

    public long CurrentGeneration
    {
        get
        {
            lock (_gate)
            {
                return _generation;
            }
        }
    }

    /// <summary>Cancels any in-flight request and returns a linked token for the new generation.</summary>
    public CancellationToken BeginNewRequest()
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            _current?.Cancel();
            _current?.Dispose();
            _generation++;
            _current = new CancellationTokenSource();
            return _current.Token;
        }
    }

    public void CancelCurrent()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _current?.Cancel();
        }
    }

    public bool IsCurrent(long generation)
    {
        lock (_gate)
        {
            return !_disposed && generation == _generation;
        }
    }

    /// <summary>Captures generation at BeginNewRequest for stale-result checks.</summary>
    public long BeginNewRequest(out CancellationToken token)
    {
        ThrowIfDisposed();
        lock (_gate)
        {
            _current?.Cancel();
            _current?.Dispose();
            _generation++;
            _current = new CancellationTokenSource();
            token = _current.Token;
            return _generation;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _current?.Cancel();
            _current?.Dispose();
            _current = null;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(SuggestionRequestCoordinator));
        }
    }
}
