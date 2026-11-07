using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using Microsoft.Extensions.Logging;
using Typee.App.ViewModels;
using Typee.App.Windows;
using Typee.Core.Abstractions;
using Typee.Core.Settings;

namespace Typee.App.Services;

public sealed class TrayIconHost : IDisposable
{
    private readonly ISettingsStore _settingsStore;
    private readonly IAppPaths _paths;
    private readonly ILogger<TrayIconHost> _logger;
    private readonly TaskbarIcon _trayIcon;
    private readonly MenuItem _pauseResumeItem;
    private SettingsWindow? _settingsWindow;
    private bool _disposed;

    public TrayIconHost(
        ISettingsStore settingsStore,
        IAppPaths paths,
        ILogger<TrayIconHost> logger)
    {
        _settingsStore = settingsStore;
        _paths = paths;
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

        var exitItem = new MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitApplication();

        var menu = new ContextMenu();
        menu.Items.Add(_pauseResumeItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(logsItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);

        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Typee – typing assistant",
            ContextMenu = menu,
            NoLeftClickDelay = true,
            Icon = CreateTrayIcon()
        };
        _trayIcon.ForceCreate();
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowSettings();

        _settingsStore.SettingsChanged += OnSettingsChanged;
        _logger.LogInformation("Tray icon ready");
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

        var viewModel = new SettingsWindowViewModel(_settingsStore, _logger);
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
