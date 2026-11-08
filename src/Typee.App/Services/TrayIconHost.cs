using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Extensions.Logging;
using Typee.App.ViewModels;
using Typee.App.Windows;
using Typee.Core.Abstractions;
using Typee.Core.Settings;
using Typee.Core.Typing;

namespace Typee.App.Services;

public sealed class TrayIconHost : IDisposable
{
    private const string DefaultTooltip = "Typee – typing assistant";

    private readonly ISettingsStore _settingsStore;
    private readonly IAppPaths _paths;
    private readonly TypingActivityService _typingActivity;
    private readonly CaretMarkerService _caretMarker;
    private readonly ILogger<TrayIconHost> _logger;
    private readonly TaskbarIcon _trayIcon;
    private readonly MenuItem _pauseResumeItem;
    private readonly MenuItem _showKeyActivityItem;
    private readonly MenuItem _showCaretMarkerItem;
    private SettingsWindow? _settingsWindow;
    private bool _showKeyActivity;
    private bool _disposed;

    public TrayIconHost(
        ISettingsStore settingsStore,
        IAppPaths paths,
        TypingActivityService typingActivity,
        CaretMarkerService caretMarker,
        ILogger<TrayIconHost> logger)
    {
        _settingsStore = settingsStore;
        _paths = paths;
        _typingActivity = typingActivity;
        _caretMarker = caretMarker;
        _logger = logger;

        _pauseResumeItem = new MenuItem
        {
            Header = settingsStore.Current.Suggestions.Enabled ? "Pause suggestions" : "Enable suggestions"
        };
        _pauseResumeItem.Click += OnPauseResumeClicked;

        var settingsItem = new MenuItem { Header = "Settings…" };
        settingsItem.Click += (_, _) => ShowSettings();

        var logsItem = new MenuItem { Header = "Open logs folder" };
        logsItem.Click += (_, _) => OpenLogsFolder();

        _showKeyActivityItem = new MenuItem { Header = "Show key activity", IsCheckable = true };
        _showKeyActivityItem.Click += OnShowKeyActivityClicked;

        _showCaretMarkerItem = new MenuItem { Header = "Show caret marker", IsCheckable = true };
        _showCaretMarkerItem.Click += OnShowCaretMarkerClicked;

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitApplication();

        var menu = new ContextMenu();
        menu.Items.Add(_pauseResumeItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(logsItem);
        menu.Items.Add(_showKeyActivityItem);
        menu.Items.Add(_showCaretMarkerItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = DefaultTooltip,
            ContextMenu = menu,
            NoLeftClickDelay = true,
            Icon = CreateTrayIcon()
        };
        _trayIcon.ForceCreate();
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowSettings();

        _settingsStore.SettingsChanged += OnSettingsChanged;
        _typingActivity.TypingPaused += OnTypingPaused;
        _typingActivity.TypingResumed += OnTypingResumed;
        _logger.LogInformation("Tray icon ready");
    }

    public void ShowWarningBalloon(string title, string message)
    {
        if (_disposed)
        {
            return;
        }

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _trayIcon.ShowNotification(title, message, NotificationIcon.Warning);
        });
    }

    public void RecreateTrayIcon()
    {
        if (_disposed)
        {
            return;
        }

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _trayIcon.ForceCreate();
            _logger.LogInformation("Tray icon recreated");
        });
    }

    public void ShowSettings()
    {
        if (_settingsWindow is { IsLoaded: true })
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
            {
                _settingsWindow.WindowState = WindowState.Normal;
            }

            _settingsWindow.Activate();
            _settingsWindow.Topmost = true;
            _settingsWindow.Topmost = false;
            _settingsWindow.Focus();
            return;
        }

        var viewModel = new SettingsWindowViewModel(_settingsStore, _paths, _logger);
        _settingsWindow = new SettingsWindow(viewModel);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settingsStore.SettingsChanged -= OnSettingsChanged;
        _typingActivity.TypingPaused -= OnTypingPaused;
        _typingActivity.TypingResumed -= OnTypingResumed;
        _trayIcon.Dispose();
        _settingsWindow?.Close();
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _pauseResumeItem.Header = settings.Suggestions.Enabled
                ? "Pause suggestions"
                : "Enable suggestions";
        });
    }

    private void OnShowCaretMarkerClicked(object sender, RoutedEventArgs e)
    {
        var enabled = _showCaretMarkerItem.IsChecked;
        _caretMarker.SetEnabled(enabled);
        _logger.LogInformation("Caret marker debug {State}", enabled ? "on" : "off");
    }

    private void OnShowKeyActivityClicked(object sender, RoutedEventArgs e)
    {
        _showKeyActivity = _showKeyActivityItem.IsChecked;
        if (!_showKeyActivity)
        {
            _trayIcon.ToolTipText = DefaultTooltip;
            _logger.LogInformation("Key activity debug off");
            return;
        }

        _trayIcon.ToolTipText = "Typee – waiting (paused)";
        _logger.LogInformation("Key activity debug on (status only, never keys)");
    }

    private void OnTypingPaused(object? sender, EventArgs e)
    {
        if (!_showKeyActivity)
        {
            return;
        }

        // Status only — never log or display virtual keys / text.
        _logger.LogInformation("Typing activity: paused");
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_showKeyActivity)
            {
                _trayIcon.ToolTipText = "Typee – waiting (paused)";
            }
        });
    }

    private void OnTypingResumed(object? sender, EventArgs e)
    {
        if (!_showKeyActivity)
        {
            return;
        }

        _logger.LogInformation("Typing activity: typing");
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_showKeyActivity)
            {
                _trayIcon.ToolTipText = "Typee – typing";
            }
        });
    }

    private async void OnPauseResumeClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var settings = _settingsStore.Current;
            settings.Suggestions.Enabled = !settings.Suggestions.Enabled;
            await _settingsStore.SaveAsync(settings).ConfigureAwait(true);
            _logger.LogInformation(
                "Suggestions {State}",
                settings.Suggestions.Enabled ? "enabled" : "paused");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle suggestions");
        }
    }

    private void OpenLogsFolder()
    {
        Directory.CreateDirectory(_paths.LogsDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = _paths.LogsDirectory,
            UseShellExecute = true
        });
    }

    private static void ExitApplication()
    {
        Application.Current.Shutdown();
    }

    private static Icon CreateTrayIcon()
    {
        var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/tray.ico"))?.Stream
            ?? throw new InvalidOperationException("Embedded tray.ico resource is missing.");
        return new Icon(stream);
    }
}
