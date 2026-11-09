using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Typee.App.Resources;
using Typee.App.ViewModels;
using Typee.App.Windows;
using Typee.Core.Abstractions;
using Typee.Core.Input;
using Typee.Core.Models;
using Typee.Core.Suggestions;
using Typee.Core.Text;
using Typee.Core.Typing;
using Typee.Platform.Windows.Process;
using Typee.Platform.Windows.Uia;
using static Typee.App.Interop.NativeMethods;

namespace Typee.App.Services;

/// <summary>
/// Owns the suggestion overlay window lifecycle: positioning, hide rules, suggestion pipeline,
/// and fullscreen pause. Never inserts text (Phase 8) and never logs suggestion content.
/// </summary>
public sealed class SuggestionOverlayService : IDisposable
{
    private const double OverlayHeightEstimatePhysical = 140;
    private const double CaretMoveTolerancePhysical = 12;

    private readonly SuggestionOverlayViewModel _viewModel;
    private readonly SuggestionPipelineService _pipeline;
    private readonly ICaretLocator _caretLocator;
    private readonly IKeyboardEvents _keyboardEvents;
    private readonly TypingActivityService _typingActivity;
    private readonly FocusMonitor _focusMonitor;
    private readonly ProcessInfoService _processInfo;
    private readonly ISettingsStore _settingsStore;
    private readonly IDiagnosticsCollector _diagnostics;
    private readonly UiLanguageService _uiLanguage;
    private readonly ILogger<SuggestionOverlayService> _logger;
    private readonly Dispatcher _dispatcher;

    private SuggestionOverlayWindow? _window;
    private DispatcherTimer? _pollTimer;
    private bool _autoDummyOnPause;
    private bool _disposed;
    private bool _leftButtonWasDown;
    private ScreenRect? _anchorCaret;
    private int _pipelineEpoch;

    public SuggestionOverlayService(
        SuggestionOverlayViewModel viewModel,
        SuggestionPipelineService pipeline,
        ICaretLocator caretLocator,
        IKeyboardEvents keyboardEvents,
        TypingActivityService typingActivity,
        FocusMonitor focusMonitor,
        ProcessInfoService processInfo,
        ISettingsStore settingsStore,
        IDiagnosticsCollector diagnostics,
        UiLanguageService uiLanguage,
        ILogger<SuggestionOverlayService> logger)
    {
        _viewModel = viewModel;
        _pipeline = pipeline;
        _caretLocator = caretLocator;
        _keyboardEvents = keyboardEvents;
        _typingActivity = typingActivity;
        _focusMonitor = focusMonitor;
        _processInfo = processInfo;
        _settingsStore = settingsStore;
        _diagnostics = diagnostics;
        _uiLanguage = uiLanguage;
        _logger = logger;
        _dispatcher = Application.Current.Dispatcher;

        _keyboardEvents.KeyEvent += OnKeyEvent;
        _typingActivity.TypingPaused += OnTypingPaused;
        _typingActivity.TypingResumed += OnTypingResumed;
        _focusMonitor.FocusChanged += OnFocusChanged;
        _settingsStore.SettingsChanged += OnSettingsChanged;
        _autoDummyOnPause = _settingsStore.Current.Suggestions.DebugDummyOnPause;
    }

    public ISuggestionOverlay Overlay => _viewModel;

    public bool AutoDummyOnPause
    {
        get => _autoDummyOnPause;
        set => _autoDummyOnPause = value;
    }

    public void ShowDummy()
    {
        _dispatcher.BeginInvoke(() =>
        {
            if (_processInfo.ShouldPauseOverlays())
            {
                _logger.LogInformation("Dummy suggestion skipped (fullscreen pause)");
                return;
            }

            var located = _caretLocator.TryLocate();
            if (located is null)
            {
                _diagnostics.RecordError("dummy-suggestion: no caret");
                _logger.LogInformation("Dummy suggestion skipped (no caret)");
                return;
            }

            _diagnostics.NoteCaretMethod(located.Method.ToString(), null);
            var suggestion = CreateDummySuggestion(located);
            ShowInternal(suggestion, located);
        });
    }

    public void Hide()
    {
        _dispatcher.BeginInvoke(() => HideInternal());
    }

    private void ShowInternal(Suggestion suggestion, CaretLocation? located)
    {
        EnsureWindow();
        _viewModel.ApplyFlowDirection(_uiLanguage.CurrentFlowDirection);
        _viewModel.Show(suggestion);
        _anchorCaret = located?.CaretRect ?? suggestion.AnchorCaret;

        var monitor = located?.Monitor ?? suggestion.Monitor;
        if (monitor is null || _anchorCaret is null)
        {
            if (!_window!.IsVisible)
            {
                _window.Show();
            }

            _window.FadeIn();
            StartPolling();
            _logger.LogInformation("Suggestion overlay shown without caret (kind={Kind})", suggestion.Kind);
            return;
        }

        var heightDip = Math.Max(_window!.ActualHeight, 120);
        var heightPhysical = heightDip * monitor.DpiY / 96.0;
        if (heightPhysical < 1)
        {
            heightPhysical = OverlayHeightEstimatePhysical;
        }

        var placed = OverlayLayoutMath.PositionBelowCaret(
            _anchorCaret.Value,
            heightPhysical,
            monitor.WorkAreaPhysical);
        _window.PlacePhysical(placed.Left, placed.Top, monitor.DpiX, monitor.DpiY);

        if (!_window.IsVisible)
        {
            _window.Show();
        }

        _window.FadeIn();
        StartPolling();
        _logger.LogInformation("Suggestion overlay shown (kind={Kind})", suggestion.Kind);
    }

    private void HideInternal()
    {
        StopPolling();
        _anchorCaret = null;
        if (_window is { IsVisible: true })
        {
            var window = _window;
            window.FadeOutAndHide(() =>
            {
                window.Hide();
                _viewModel.Hide();
            });
        }
        else
        {
            _viewModel.Hide();
        }
    }

    private void EnsureWindow()
    {
        if (_window is not null)
        {
            return;
        }

        _window = new SuggestionOverlayWindow(_viewModel);
        _window.Show();
        _window.Hide();
        _window.Opacity = 1;
    }

    private void StartPolling()
    {
        _pollTimer ??= new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(80)
        };
        _pollTimer.Tick -= OnPollTick;
        _pollTimer.Tick += OnPollTick;
        _pollTimer.Start();
        _leftButtonWasDown = false;
    }

    private void StopPolling()
    {
        if (_pollTimer is null)
        {
            return;
        }

        _pollTimer.Stop();
        _pollTimer.Tick -= OnPollTick;
    }

    private void OnPollTick(object? sender, EventArgs e)
    {
        if (!_viewModel.IsVisible)
        {
            StopPolling();
            return;
        }

        if (_processInfo.ShouldPauseOverlays() || _processInfo.GetSkipReason() is not null)
        {
            HideInternal();
            return;
        }

        CheckOutsideClick();
        CheckCaretMoved();
    }

    private void CheckOutsideClick()
    {
        var down = (GetAsyncKeyState(VkLbutton) & 0x8000) != 0;
        if (down && !_leftButtonWasDown)
        {
            if (GetCursorPos(out var pt) && _window is not null && _viewModel.Current?.Monitor is { } monitor)
            {
                if (!_window.ContainsPhysicalPoint(pt.X, pt.Y, monitor.DpiX, monitor.DpiY))
                {
                    HideInternal();
                }
            }
        }

        _leftButtonWasDown = down;
    }

    private void CheckCaretMoved()
    {
        if (_anchorCaret is not { } anchor)
        {
            return;
        }

        var located = _caretLocator.TryLocate();
        if (located is null)
        {
            HideInternal();
            return;
        }

        var dx = Math.Abs(located.CaretRect.Left - anchor.Left);
        var dy = Math.Abs(located.CaretRect.Top - anchor.Top);
        if (dx > CaretMoveTolerancePhysical || dy > CaretMoveTolerancePhysical)
        {
            HideInternal();
        }
    }

    private void OnKeyEvent(object? sender, KeyboardEvent e)
    {
        if (!e.IsKeyDown || !_viewModel.IsVisible)
        {
            return;
        }

        var modifiers = e.Modifiers;
        _dispatcher.BeginInvoke(() =>
        {
            _ = _viewModel.HandleKey(e.VirtualKey, modifiers);
            if (!_viewModel.IsVisible)
            {
                StopPolling();
                _window?.Hide();
            }
        });
    }

    private void OnTypingPaused(object? sender, EventArgs e)
    {
        if (!_settingsStore.Current.Suggestions.Enabled)
        {
            return;
        }

        if (_autoDummyOnPause || _settingsStore.Current.Suggestions.DebugDummyOnPause)
        {
            ShowDummy();
            return;
        }

        var epoch = Interlocked.Increment(ref _pipelineEpoch);
        _ = RunPipelineAsync(epoch);
    }

    private async Task RunPipelineAsync(int epoch)
    {
        try
        {
            var result = await _pipeline.RunAsync().ConfigureAwait(false);
            if (epoch != Volatile.Read(ref _pipelineEpoch))
            {
                return;
            }

            if (!result.IsSuccess || result.Suggestion is null)
            {
                if (result.Outcome is not ("cancelled" or "stale" or "too-short" or "no-match" or "no-context"))
                {
                    _logger.LogInformation("Pipeline outcome={Outcome}", result.Outcome);
                }

                return;
            }

            var suggestion = result.Suggestion;
            await _dispatcher.InvokeAsync(() =>
            {
                if (epoch != Volatile.Read(ref _pipelineEpoch))
                {
                    return;
                }

                if (_processInfo.ShouldPauseOverlays() || _processInfo.GetSkipReason() is not null)
                {
                    return;
                }

                var located = _caretLocator.TryLocate();
                if (located is not null)
                {
                    _diagnostics.NoteCaretMethod(located.Method.ToString(), null);
                }

                ShowInternal(suggestion, located);
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pipeline run failed");
            _diagnostics.RecordError($"pipeline:{ex.GetType().Name}");
        }
    }

    private void OnTypingResumed(object? sender, EventArgs e)
    {
        Interlocked.Increment(ref _pipelineEpoch);
        _pipeline.Cancel();
        // Hide/trim is driven by OnKeyEvent (first key of the resume).
    }

    private void OnFocusChanged(object? sender, EventArgs e)
    {
        Interlocked.Increment(ref _pipelineEpoch);
        _pipeline.Cancel();
        if (!_viewModel.IsVisible)
        {
            return;
        }

        _dispatcher.BeginInvoke(HideInternal);
    }

    private void OnSettingsChanged(object? sender, Core.Settings.AppSettings settings)
    {
        _autoDummyOnPause = settings.Suggestions.DebugDummyOnPause;
        if (!_viewModel.IsVisible)
        {
            return;
        }

        if (!settings.Suggestions.Enabled
            || (settings.PauseInFullscreen && _processInfo.ShouldPauseOverlays()))
        {
            _dispatcher.BeginInvoke(HideInternal);
        }
    }

    private static Suggestion CreateDummySuggestion(CaretLocation located) =>
        new()
        {
            HeaderKey = nameof(Strings.Suggestion_GrammarFix),
            Body = "This is a test of the app",
            Prefix = "This is a ",
            Highlight = "test",
            Suffix = " of the app",
            Options = ["This is a test of the app", "This is a trial of the app", "This is a demo of the app"],
            Kind = SuggestionKind.GrammarFix,
            Level = SuggestionLevel.Grammar,
            AnchorCaret = located.CaretRect,
            Monitor = located.Monitor
        };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Interlocked.Increment(ref _pipelineEpoch);
        _pipeline.Cancel();
        _keyboardEvents.KeyEvent -= OnKeyEvent;
        _typingActivity.TypingPaused -= OnTypingPaused;
        _typingActivity.TypingResumed -= OnTypingResumed;
        _focusMonitor.FocusChanged -= OnFocusChanged;
        _settingsStore.SettingsChanged -= OnSettingsChanged;

        _dispatcher.Invoke(() =>
        {
            StopPolling();
            _viewModel.Hide();
            _window?.Close();
            _window = null;
        });
    }
}
