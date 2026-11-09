using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Typee.App.Interop;
using Typee.App.Services;
using Typee.App.SingleInstance;
using Typee.App.ViewModels;
using Typee.Core;
using Typee.Core.Abstractions;
using Typee.Core.Reliability;
using Typee.Core.Typing;
using Typee.App.Windows;
using Typee.Core.Llm;
using Typee.Grammar;
using Typee.Llm;
using Typee.Platform.Windows;
using Typee.Platform.Windows.Keyboard;
using Typee.Platform.Windows.SystemEvents;
using Typee.Storage;
using static Typee.App.Interop.NativeMethods;

namespace Typee.App;

public partial class App : Application
{
    private IHost? _host;
    private SingleInstanceGuard? _singleInstance;
    private TrayIconHost? _trayIconHost;
    private SuggestionOverlayService? _suggestionOverlay;
    private ServiceSupervisor? _hookSupervisor;
    private SystemEventMonitor? _systemEventMonitor;
    private IGrammarChecker? _grammar;
    private ILlmClient? _llm;
    private ILlmRateGuard? _rateGuard;
    private static TrayIconHost? _trayForErrors;
    private static IDiagnosticsCollector? _diagnosticsForErrors;

    protected override async void OnStartup(StartupEventArgs e)
    {
        if (!SetDefaultDllDirectories(LoadLibrarySearchDefaultDirs))
        {
            _ = Marshal.GetLastWin32Error();
        }

        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var paths = new TypeePaths();
        paths.EnsureCreated();
        CrashDiagnostics.OfferDiagnosticReportIfNeeded(paths);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(paths.LogsDirectory, "typee-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _singleInstance = SingleInstanceGuard.Acquire(OnActivateSettingsRequested);
        if (!_singleInstance.IsPrimaryInstance)
        {
            _singleInstance.SignalActivateSettings();
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        try
        {
            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IAppPaths>(paths);
                    services.AddSingleton(paths);
                    services.AddTypeeStorage();
                    services.AddTypeeGrammar();
                    services.AddTypeePlatformWindows();
                    services.AddTypeeLlm();
                    services.AddSingleton<KeyboardInterceptionState>();
                    services.AddSingleton<IRawKeyboardActivityClock, RawKeyboardActivityClock>();
                    services.AddSingleton<ISessionLockState, SessionLockState>();
                    services.AddSingleton<KeyboardHookService>();
                    services.AddSingleton<IKeyboardEvents>(sp => sp.GetRequiredService<KeyboardHookService>());
                    services.AddSingleton<IHotkeyService, HotkeyService>();
                    services.AddSingleton<TypingActivityService>(sp => new TypingActivityService(
                        sp.GetRequiredService<IKeyboardEvents>(),
                        TimeProvider.System,
                        sp.GetRequiredService<ISettingsStore>().Current.Suggestions.DebounceMs));
                    services.AddSingleton<HotkeyCoordinator>();
                    services.AddSingleton<CaretMarkerService>();
                    services.AddSingleton<UiLanguageService>();
                    services.AddSingleton<IDiagnosticsCollector, DiagnosticsCollector>();
                    services.AddSingleton<SuggestionOverlayViewModel>();
                    services.AddSingleton<ISuggestionOverlay>(sp => sp.GetRequiredService<SuggestionOverlayViewModel>());
                    services.AddSingleton<SuggestionOverlayService>();
                    services.AddSingleton<TrayIconHost>();
                    services.AddSingleton<SystemEventMonitor>();
                })
                .Build();

            var settingsStore = _host.Services.GetRequiredService<ISettingsStore>();
            await settingsStore.LoadAsync().ConfigureAwait(true);
            await OfferConsentIfNeededAsync(settingsStore).ConfigureAwait(true);

            _diagnosticsForErrors = _host.Services.GetRequiredService<IDiagnosticsCollector>();

            try
            {
                await _host.Services.GetRequiredService<ITypeeDatabase>()
                    .InitializeAsync()
                    .ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "SQLite initialization failed — continuing without local learning store");
                _diagnosticsForErrors.RecordError($"sqlite-init:{ex.GetType().Name}");
            }

            _ = _host.Services.GetRequiredService<UiLanguageService>();

            var keyboardHook = _host.Services.GetRequiredService<KeyboardHookService>();
            var loggerFactory = _host.Services.GetRequiredService<ILoggerFactory>();
            var appLogger = loggerFactory.CreateLogger<App>();

            _hookSupervisor = new ServiceSupervisor(
                "keyboard-hook",
                start: () => keyboardHook.Start(),
                stop: () => keyboardHook.Reinstall(),
                appLogger);
            keyboardHook.HookStoppedUnexpectedly += (_, _) =>
                _hookSupervisor.NotifyStoppedUnexpectedly("hook thread exited");
            _hookSupervisor.GiveUp += (_, args) =>
            {
                _diagnosticsForErrors?.RecordError($"hook give-up:{args.FeatureName}");
                _trayForErrors?.ShowWarningBalloon(
                    "Typee degraded",
                    $"Keyboard input ({args.FeatureName}) stopped after repeated failures.");
            };
            _hookSupervisor.Restarting += (_, args) =>
                _trayForErrors?.ShowWarningBalloon(
                    "Typee",
                    $"Restarting {args.FeatureName}…");
            _hookSupervisor.StartInitial();

            _host.Services.GetRequiredService<TypingActivityService>()
                .Configure(_host.Services.GetRequiredService<ISettingsStore>().Current.Suggestions.DebounceMs);
            _host.Services.GetRequiredService<HotkeyCoordinator>()
                .ApplyFromSettings(_host.Services.GetRequiredService<ISettingsStore>().Current);

            _suggestionOverlay = _host.Services.GetRequiredService<SuggestionOverlayService>();
            _trayIconHost = _host.Services.GetRequiredService<TrayIconHost>();
            _trayForErrors = _trayIconHost;

            _grammar = _host.Services.GetRequiredService<IGrammarChecker>();
            _grammar.StatusChanged += OnGrammarStatusChanged;
            try
            {
                await _grammar.StartAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Grammar subsystem failed to start — continuing with fallback");
                _diagnosticsForErrors?.RecordError($"grammar-start:{ex.GetType().Name}");
            }

            _llm = _host.Services.GetRequiredService<ILlmClient>();
            _llm.StatusChanged += OnLlmStatusChanged;
            _rateGuard = _host.Services.GetRequiredService<ILlmRateGuard>();
            _rateGuard.Paused += OnLlmRateGuardPaused;

            _systemEventMonitor = _host.Services.GetRequiredService<SystemEventMonitor>();
            _systemEventMonitor.SystemResumed += OnSystemResumed;
            _systemEventMonitor.TaskbarCreated += OnTaskbarCreated;

            CrashDiagnostics.WriteRunningMarker(paths);

            var logger = _host.Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Typee started");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Typee failed to start");
            MessageBox.Show(
                "Typee failed to start. Check the log folder under %LOCALAPPDATA%\\Typee\\logs.",
                "Typee",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _systemEventMonitor?.Dispose();
        _systemEventMonitor = null;
        _hookSupervisor?.Dispose();
        _hookSupervisor = null;

        if (_grammar is not null)
        {
            _grammar.StatusChanged -= OnGrammarStatusChanged;
            // Host dispose tears down IGrammarChecker (IAsyncDisposable).
            _grammar = null;
        }

        if (_llm is not null)
        {
            _llm.StatusChanged -= OnLlmStatusChanged;
            _llm = null;
        }

        if (_rateGuard is not null)
        {
            _rateGuard.Paused -= OnLlmRateGuardPaused;
            _rateGuard = null;
        }

        _trayForErrors = null;
        _diagnosticsForErrors = null;
        _trayIconHost?.Dispose();
        _trayIconHost = null;
        _suggestionOverlay?.Dispose();
        _suggestionOverlay = null;
        _singleInstance?.Dispose();
        _singleInstance = null;

        if (_host is not null)
        {
            if (_host.Services.GetService<IAppPaths>() is { } paths)
            {
                CrashDiagnostics.ClearMarker(paths);
            }

            if (_host is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync().ConfigureAwait(true);
            }
            else
            {
                _host.Dispose();
            }

            _host = null;
        }

        await Log.CloseAndFlushAsync().ConfigureAwait(true);
        base.OnExit(e);
    }

    private void OnSystemResumed(object? sender, EventArgs e)
    {
        if (_host is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(async () =>
        {
            try
            {
                _host.Services.GetRequiredService<HotkeyCoordinator>().OnSystemResumed();
                if (_grammar is not null)
                {
                    await _grammar.EnsureAliveAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to handle system resume");
            }
        });
    }

    private void OnGrammarStatusChanged(object? sender, EventArgs e)
    {
        if (_grammar is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (_grammar is { IsDisabled: true })
            {
                _trayForErrors?.ShowWarningBalloon(
                    "Typee grammar disabled",
                    "harper-ls restarted too many times. Using built-in fallback checker.");
                _trayForErrors?.SetDegradedTooltip("Typee – grammar degraded");
                _diagnosticsForErrors?.RecordError("grammar:disabled-restart-budget");
            }
        });
    }

    private void OnLlmStatusChanged(object? sender, EventArgs e)
    {
        if (_llm is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (_llm is { IsOnline: false })
            {
                _trayForErrors?.ShowWarningBalloon(
                    "Typee LLM offline",
                    "Remote completions paused. Local grammar and phrases still work.");
                _trayForErrors?.SetDegradedTooltip("Typee – LLM offline");
                _diagnosticsForErrors?.RecordError($"llm:offline:{_llm.Status}");
            }
        });
    }

    private void OnLlmRateGuardPaused(object? sender, LlmRateGuardPausedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _trayForErrors?.ShowWarningBalloon(
                "Typee LLM paused",
                $"Request limit reached ({e.Reason}). Local features still work.");
            _trayForErrors?.SetDegradedTooltip("Typee – LLM rate limited");
            _diagnosticsForErrors?.RecordError($"llm:rate-guard:{e.Reason}");
        });
    }

    private static async Task OfferConsentIfNeededAsync(ISettingsStore settingsStore)
    {
        var settings = settingsStore.Current;
        if (settings.Privacy.LlmConsentAccepted)
        {
            return;
        }

        var dialog = new ConsentWindow();
        var result = dialog.ShowDialog();
        if (result == true && dialog.Accepted)
        {
            settings.Privacy.LlmConsentAccepted = true;
            await settingsStore.SaveAsync(settings).ConfigureAwait(true);
            Log.Information("LLM privacy consent accepted");
        }
        else
        {
            Log.Information("LLM privacy consent deferred — local features only");
        }
    }

    private void OnTaskbarCreated(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() => _trayIconHost?.RecreateTrayIcon());
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs args)
    {
        Log.Fatal(args.Exception, "Unhandled dispatcher exception");
        _diagnosticsForErrors?.RecordError($"dispatcher:{args.Exception.GetType().Name}");
        _trayForErrors?.ShowWarningBalloon("Typee error", "A recoverable error occurred. Typee will keep running.");
        args.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "Unhandled domain exception (terminating={IsTerminating})", args.IsTerminating);
            _diagnosticsForErrors?.RecordError($"domain:{ex.GetType().Name}");
            _trayForErrors?.ShowWarningBalloon("Typee error", "A serious error occurred.");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        Log.Fatal(args.Exception, "Unobserved task exception");
        _diagnosticsForErrors?.RecordError($"task:{args.Exception.GetType().Name}");
        _trayForErrors?.ShowWarningBalloon("Typee error", "A background task failed.");
        args.SetObserved();
    }

    private void OnActivateSettingsRequested()
    {
        Dispatcher.Invoke(() => _trayIconHost?.ShowSettings());
    }
}
