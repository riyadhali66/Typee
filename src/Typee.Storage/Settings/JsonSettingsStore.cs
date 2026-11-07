using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;
using Typee.Core.Settings;

namespace Typee.Storage.Settings;

public sealed class JsonSettingsStore : ISettingsStore, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly IAppPaths _paths;
    private readonly ILogger<JsonSettingsStore> _logger;
    private readonly object _gate = new();
    private readonly FileSystemWatcher _watcher;
    private AppSettings _current = AppSettings.CreateDefault();
    private bool _suppressWatcher;
    private bool _disposed;

    public JsonSettingsStore(IAppPaths paths, ILogger<JsonSettingsStore> logger)
    {
        _paths = paths;
        _logger = logger;
        SettingsPath = paths.SettingsFilePath;

        Directory.CreateDirectory(paths.RootDirectory);

        _watcher = new FileSystemWatcher(paths.RootDirectory, "settings.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            EnableRaisingEvents = true
        };
        _watcher.Changed += OnSettingsFileChanged;
        _watcher.Created += OnSettingsFileChanged;
        _watcher.Renamed += OnSettingsFileChanged;
    }

    public string SettingsPath { get; }

    public AppSettings Current
    {
        get
        {
            lock (_gate)
            {
                return Clone(_current);
            }
        }
    }

    public event EventHandler<AppSettings>? SettingsChanged;

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(SettingsPath))
        {
            var defaults = AppSettings.CreateDefault();
            await SaveAsync(defaults, cancellationToken).ConfigureAwait(false);
            return defaults;
        }

        await using var stream = new FileStream(
            SettingsPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false) ?? AppSettings.CreateDefault();

        Normalize(loaded);

        lock (_gate)
        {
            _current = loaded;
        }

        _logger.LogInformation("Loaded settings from {SettingsPath}", SettingsPath);
        return Clone(loaded);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Normalize(settings);

        Directory.CreateDirectory(_paths.RootDirectory);
        var temporaryPath = SettingsPath + ".tmp";
        await using (var stream = new FileStream(
                         temporaryPath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, settings, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        _suppressWatcher = true;
        try
        {
            File.Copy(temporaryPath, SettingsPath, overwrite: true);
            File.Delete(temporaryPath);
        }
        finally
        {
            await Task.Delay(75, cancellationToken).ConfigureAwait(false);
            _suppressWatcher = false;
        }

        lock (_gate)
        {
            _current = Clone(settings);
        }

        _logger.LogInformation("Saved settings to {SettingsPath}", SettingsPath);
        SettingsChanged?.Invoke(this, Clone(settings));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.Changed -= OnSettingsFileChanged;
        _watcher.Created -= OnSettingsFileChanged;
        _watcher.Renamed -= OnSettingsFileChanged;
        _watcher.Dispose();
    }

    private void OnSettingsFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_suppressWatcher)
        {
            return;
        }

        _ = ReloadFromDiskSafeAsync();
    }

    private async Task ReloadFromDiskSafeAsync()
    {
        try
        {
            await Task.Delay(120).ConfigureAwait(false);
            if (_suppressWatcher || !File.Exists(SettingsPath))
            {
                return;
            }

            await using var stream = new FileStream(
                SettingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var loaded = await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions)
                .ConfigureAwait(false);
            if (loaded is null)
            {
                return;
            }

            Normalize(loaded);
            lock (_gate)
            {
                _current = loaded;
            }

            _logger.LogInformation("Hot-reloaded settings from {SettingsPath}", SettingsPath);
            SettingsChanged?.Invoke(this, Clone(loaded));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to hot-reload settings from {SettingsPath}", SettingsPath);
        }
    }

    private static void Normalize(AppSettings settings)
    {
        settings.Hotkeys ??= new HotkeySettings();
        settings.Suggestions ??= new SuggestionSettings();
        settings.Grammar ??= new GrammarSettings();
        settings.Llm ??= new LlmSettings();
        settings.Speech ??= new SpeechSettings();
        settings.BlockedProcesses ??= AppSettings.DefaultBlockedProcesses();
        settings.Grammar.DisabledRules ??= [];
        settings.Suggestions.DebounceMs = Math.Clamp(settings.Suggestions.DebounceMs, 50, 10_000);
        settings.Suggestions.MinCharsBeforeSuggest = Math.Clamp(settings.Suggestions.MinCharsBeforeSuggest, 1, 10_000);
        settings.Suggestions.MaxContextChars = Math.Clamp(settings.Suggestions.MaxContextChars, 32, 20_000);
        settings.Grammar.RequestTimeoutMs = Math.Clamp(settings.Grammar.RequestTimeoutMs, 50, 10_000);
        settings.Llm.TimeoutMs = Math.Clamp(settings.Llm.TimeoutMs, 500, 120_000);
        settings.Llm.MaxTokens = Math.Clamp(settings.Llm.MaxTokens, 1, 2048);
    }

    private static AppSettings Clone(AppSettings source)
    {
        var json = JsonSerializer.Serialize(source, SerializerOptions);
        return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? AppSettings.CreateDefault();
    }
}
