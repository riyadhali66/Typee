using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Typee.App.Windows;
using Typee.Core.Abstractions;

namespace Typee.App.Services;

public sealed class CaretMarkerService : IDisposable
{
    private readonly ICaretLocator _caretLocator;
    private readonly ILogger<CaretMarkerService> _logger;
    private readonly Dispatcher _dispatcher;
    private CaretMarkerWindow? _marker;
    private DispatcherTimer? _timer;
    private bool _disposed;

    public CaretMarkerService(ICaretLocator caretLocator, ILogger<CaretMarkerService> logger)
    {
        _caretLocator = caretLocator;
        _logger = logger;
        _dispatcher = System.Windows.Application.Current.Dispatcher;
    }

    public void SetEnabled(bool enabled)
    {
        _dispatcher.Invoke(() =>
        {
            if (enabled)
            {
                Start();
            }
            else
            {
                Stop();
            }
        });
    }

    private void Start()
    {
        _marker ??= new CaretMarkerWindow();
        _timer ??= new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();
        if (!_marker.IsVisible)
        {
            _marker.Show();
        }

        _logger.LogInformation("Caret marker debug enabled");
    }

    private void Stop()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer.Tick -= OnTick;
        }

        _marker?.Hide();
        _logger.LogInformation("Caret marker debug disabled");
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var located = _caretLocator.TryLocate();
        if (located is null)
        {
            _marker?.Hide();
            return;
        }

        _marker ??= new CaretMarkerWindow();
        if (!_marker.IsVisible)
        {
            _marker.Show();
        }

        var rect = located.CaretRect;
        _marker.MoveToPhysical(rect.Left, rect.Top, rect.Width, rect.Height, located.Monitor.DpiX, located.Monitor.DpiY);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dispatcher.Invoke(() =>
        {
            Stop();
            _marker?.Close();
            _marker = null;
        });
    }
}
