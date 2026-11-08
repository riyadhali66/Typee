using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Typee.App.Services;
using Typee.App.SingleInstance;
using Typee.Core;
using Typee.Core.Abstractions;
using Typee.Core.Typing;
using Typee.Platform.Windows.Keyboard;
using Typee.Storage;

namespace Typee.App;

public partial class App : Application
{
    private IHost? _host;
    private SingleInstanceGuard? _singleInstance;
    private TrayIconHost? _trayIconHost;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var paths = new TypeePaths();
        paths.EnsureCreated();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(paths.LogsDirectory, "typee-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true)
            .CreateLogger();

        DispatcherUnhandledException += (_, args) =>
        {
            Log.Fatal(args.Exception, "Unhandled dispatcher exception");
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "Unhandled domain exception");
            }
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Fatal(args.Exception, "Unhandled task exception");
            args.SetObserved();
        };

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
            // UseSerilog + DI composition root. Do not StartAsync the default console
            // lifetime — it tears down (and disposes WPF tray objects) when no console is attached.
            _host = Host.CreateDefaultBuilder()
                .UseSerilog()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IAppPaths>(paths);
                    services.AddSingleton(paths);
                    services.AddTypeeStorage();
                    services.AddSingleton<KeyboardInterceptionState>();
                    services.AddSingleton<KeyboardHookService>();
                    services.AddSingleton<IKeyboardEvents>(sp => sp.GetRequiredService<KeyboardHookService>());
                    services.AddSingleton<IHotkeyService, HotkeyService>();
                    services.AddSingleton<TypingActivityService>(sp => new TypingActivityService(
                        sp.GetRequiredService<IKeyboardEvents>(),
                        TimeProvider.System,
                        sp.GetRequiredService<ISettingsStore>().Current.Suggestions.DebounceMs));
                    services.AddSingleton<HotkeyCoordinator>();
                    services.AddSingleton<TrayIconHost>();
                })
                .Build();

            var settingsStore = _host.Services.GetRequiredService<ISettingsStore>();
            await settingsStore.LoadAsync().ConfigureAwait(true);

            // Input stack: hook first, then consumers, then hotkey registration.
            _host.Services.GetRequiredService<KeyboardHookService>().Start();
            _host.Services.GetRequiredService<TypingActivityService>()
                .Configure(_host.Services.GetRequiredService<ISettingsStore>().Current.Suggestions.DebounceMs);
            _host.Services.GetRequiredService<HotkeyCoordinator>()
                .ApplyFromSettings(_host.Services.GetRequiredService<ISettingsStore>().Current);

            _trayIconHost = _host.Services.GetRequiredService<TrayIconHost>();
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
        _trayIconHost?.Dispose();
        _trayIconHost = null;
        _singleInstance?.Dispose();
        _singleInstance = null;

        if (_host is not null)
        {
            // Dispose DI graph after tray is already torn down on the UI thread.
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

    private void OnActivateSettingsRequested()
    {
        Dispatcher.Invoke(() => _trayIconHost?.ShowSettings());
    }
}
