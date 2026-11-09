using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Typee.App.Resources;
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
    private readonly IPhraseService _phrases;
    private readonly IUserDictionaryRepository _dictionary;
    private readonly ITypeeDatabase _database;
    private readonly IApiKeyStore _apiKeys;
    private readonly ILlmClient _llm;
    private readonly TypingActivityService _typingActivity;
    private readonly CaretMarkerService _caretMarker;
    private readonly SuggestionOverlayService _suggestionOverlay;
    private readonly IDiagnosticsCollector _diagnostics;
    private readonly UiLanguageService _uiLanguage;
    private readonly ILogger<TrayIconHost> _logger;
    private readonly TaskbarIcon _trayIcon;
    private readonly MenuItem _pauseResumeItem;
    private readonly MenuItem _showKeyActivityItem;
    private readonly MenuItem _showCaretMarkerItem;
    private readonly MenuItem _autoDummyItem;
    private SettingsWindow? _settingsWindow;
    private DiagnosticsWindow? _diagnosticsWindow;
    private Icon? _currentTrayIcon;
    private bool _forcePausedIcon;
    private bool _showKeyActivity;
    private bool _disposed;

    public TrayIconHost(
        ISettingsStore settingsStore,
        IAppPaths paths,
        IPhraseService phrases,
        IUserDictionaryRepository dictionary,
        ITypeeDatabase database,
        IApiKeyStore apiKeys,
        ILlmClient llm,
        TypingActivityService typingActivity,
        CaretMarkerService caretMarker,
        SuggestionOverlayService suggestionOverlay,
        IDiagnosticsCollector diagnostics,
        UiLanguageService uiLanguage,
        ILogger<TrayIconHost> logger)
    {
        _settingsStore = settingsStore;
        _paths = paths;
        _phrases = phrases;
        _dictionary = dictionary;
        _database = database;
        _apiKeys = apiKeys;
        _llm = llm;
        _typingActivity = typingActivity;
        _caretMarker = caretMarker;
        _suggestionOverlay = suggestionOverlay;
        _diagnostics = diagnostics;
        _uiLanguage = uiLanguage;
        _logger = logger;

        _pauseResumeItem = new MenuItem
        {
            Header = settingsStore.Current.Suggestions.Enabled
                ? Strings.Tray_PauseSuggestions
                : Strings.Tray_EnableSuggestions
        };
        _pauseResumeItem.Click += OnPauseResumeClicked;

        var settingsItem = new MenuItem { Header = Strings.Tray_Settings };
        settingsItem.Click += (_, _) => ShowSettings();

        var diagnosticsItem = new MenuItem { Header = Strings.Tray_Diagnostics };
        diagnosticsItem.Click += (_, _) => ShowDiagnostics();

        var logsItem = new MenuItem { Header = Strings.Tray_OpenLogs };
        logsItem.Click += (_, _) => OpenLogsFolder();

        var dummyItem = new MenuItem { Header = Strings.Tray_ShowDummySuggestion };
        dummyItem.Click += (_, _) =>
        {
            _logger.LogInformation("Tray: show dummy suggestion");
            _suggestionOverlay.ShowDummy();
        };

        _autoDummyItem = new MenuItem
        {
            Header = Strings.Tray_AutoDummyOnPause,
            IsCheckable = true,
            IsChecked = settingsStore.Current.Suggestions.DebugDummyOnPause
        };
        _autoDummyItem.Click += OnAutoDummyClicked;

        _showKeyActivityItem = new MenuItem { Header = Strings.Tray_ShowKeyActivity, IsCheckable = true };
        _showKeyActivityItem.Click += OnShowKeyActivityClicked;

        _showCaretMarkerItem = new MenuItem { Header = Strings.Tray_ShowCaretMarker, IsCheckable = true };
        _showCaretMarkerItem.Click += OnShowCaretMarkerClicked;

        var exitItem = new MenuItem { Header = Strings.Tray_Exit };
        exitItem.Click += (_, _) => ExitApplication();

        var menu = new ContextMenu
        {
            FlowDirection = _uiLanguage.CurrentFlowDirection
        };
        menu.Items.Add(_pauseResumeItem);
        menu.Items.Add(settingsItem);
        menu.Items.Add(diagnosticsItem);
        menu.Items.Add(logsItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(dummyItem);
        menu.Items.Add(_autoDummyItem);
        menu.Items.Add(_showKeyActivityItem);
        menu.Items.Add(_showCaretMarkerItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(exitItem);

        _currentTrayIcon = LoadTrayIconForCurrentState();
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = DefaultTooltip,
            ContextMenu = menu,
            NoLeftClickDelay = true,
            Icon = _currentTrayIcon
        };
        _trayIcon.ForceCreate();
        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowSettings();

        _settingsStore.SettingsChanged += OnSettingsChanged;
        _typingActivity.TypingPaused += OnTypingPaused;
        _typingActivity.TypingResumed += OnTypingResumed;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
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

    /// <summary>Called from App when grammar (or another subsystem) degrades.</summary>
    public void SetDegradedTooltip(string tooltip)
    {
        if (_disposed || string.IsNullOrWhiteSpace(tooltip))
        {
            return;
        }

        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            _forcePausedIcon = true;
            _trayIcon.ToolTipText = tooltip;
            ApplyTrayIcon();
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
            ApplyTrayIcon();
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

        var viewModel = new SettingsWindowViewModel(
            _settingsStore,
            _paths,
            _phrases,
            _dictionary,
            _database,
            _apiKeys,
            _llm,
            _logger);
        _settingsWindow = new SettingsWindow(viewModel);
        _settingsWindow.FlowDirection = _uiLanguage.CurrentFlowDirection;
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void ShowDiagnostics()
    {
        if (_diagnosticsWindow is { IsLoaded: true })
        {
            _diagnosticsWindow.Activate();
            return;
        }

        var viewModel = new DiagnosticsViewModel(_diagnostics);
        _diagnosticsWindow = new DiagnosticsWindow(viewModel);
        _diagnosticsWindow.FlowDirection = _uiLanguage.CurrentFlowDirection;
        _diagnosticsWindow.Closed += (_, _) => _diagnosticsWindow = null;
        _diagnosticsWindow.Show();
        _diagnosticsWindow.Activate();
        _logger.LogInformation("Diagnostics window opened");
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
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _trayIcon.Dispose();
        _currentTrayIcon?.Dispose();
        _currentTrayIcon = null;
        _settingsWindow?.Close();
        _diagnosticsWindow?.Close();
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (settings.Suggestions.Enabled)
            {
                _forcePausedIcon = false;
            }

            _pauseResumeItem.Header = settings.Suggestions.Enabled
                ? Strings.Tray_PauseSuggestions
                : Strings.Tray_EnableSuggestions;
            _autoDummyItem.IsChecked = settings.Suggestions.DebugDummyOnPause;
            _suggestionOverlay.AutoDummyOnPause = settings.Suggestions.DebugDummyOnPause;
            ApplyTrayIcon();
        });
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color))
        {
            return;
        }

        Application.Current?.Dispatcher.BeginInvoke(ApplyTrayIcon);
    }

    private void OnShowCaretMarkerClicked(object sender, RoutedEventArgs e)
    {
        var enabled = _showCaretMarkerItem.IsChecked;
        _caretMarker.SetEnabled(enabled);
        _logger.LogInformation("Caret marker debug {State}", enabled ? "on" : "off");
    }

    private async void OnAutoDummyClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var enabled = _autoDummyItem.IsChecked;
            _suggestionOverlay.AutoDummyOnPause = enabled;
            var settings = _settingsStore.Current;
            settings.Suggestions.DebugDummyOnPause = enabled;
            await _settingsStore.SaveAsync(settings).ConfigureAwait(true);
            _logger.LogInformation("Auto dummy on pause {State}", enabled ? "on" : "off");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle auto dummy");
            _diagnostics.RecordError("auto-dummy toggle failed");
        }
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
            _diagnostics.RecordError("suggestions toggle failed");
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

    private void ApplyTrayIcon()
    {
        if (_disposed)
        {
            return;
        }

        var previous = _currentTrayIcon;
        var next = LoadTrayIconForCurrentState();
        _currentTrayIcon = next;
        _trayIcon.Icon = next;
        previous?.Dispose();
    }

    private Icon LoadTrayIconForCurrentState()
    {
        var paused = _forcePausedIcon || !_settingsStore.Current.Suggestions.Enabled;
        var resource = paused
            ? "Assets/typee-tray-paused.ico"
            : SystemUsesLightTheme()
                ? "Assets/typee-tray-black.ico"
                : "Assets/typee-tray-white.ico";

        var stream = Application.GetResourceStream(new Uri($"pack://application:,,,/{resource}"))?.Stream
            ?? Application.GetResourceStream(new Uri("pack://application:,,,/Assets/tray.ico"))?.Stream
            ?? throw new InvalidOperationException("Embedded tray icon resource is missing.");

        return new Icon(stream);
    }

    /// <summary>
    /// Windows 10/11: 1 = light taskbar (use black glyph), 0 = dark taskbar (use white glyph).
    /// Missing key → prefer white (Windows 11 dark default).
    /// </summary>
    private static bool SystemUsesLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("SystemUsesLightTheme");
            return value is int i && i == 1;
        }
        catch
        {
            return false;
        }
    }
}
