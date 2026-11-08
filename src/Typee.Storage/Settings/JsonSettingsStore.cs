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
    private List<string> _lastLoadNotices = [];
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

    public IReadOnlyList<string> LastLoadNotices
    {
        get
        {
            lock (_gate)
            {
                return _lastLoadNotices.ToList();
            }
        }
    }

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
        var notices = new List<string>();

        if (!File.Exists(SettingsPath))
        {
            if (!TryRestoreFromBackup(notices))
            {
                var defaults = AppSettings.CreateDefault();
                await SaveAsync(defaults, cancellationToken).ConfigureAwait(false);
                ApplyNotices(notices);
                return defaults;
            }
        }

        AppSettings loaded;
        try
        {
            loaded = await ReadSettingsFileAsync(SettingsPath, cancellationToken).ConfigureAwait(false)
                     ?? AppSettings.CreateDefault();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "settings.json unreadable; attempting backup restore");
            if (!TryRestoreFromBackup(notices))
            {
                loaded = AppSettings.CreateDefault();
                notices.Add("settings.json was corrupt and no backup was available — using defaults.");
            }
            else
            {
                loaded = await ReadSettingsFileAsync(SettingsPath, cancellationToken).ConfigureAwait(false)
                         ?? AppSettings.CreateDefault();
            }
        }

        NormalizeNulls(loaded);
        var migrated = SettingsMigrator.Migrate(loaded);
        if (migrated)
        {
            notices.Add($"Migrated settings to schema version {SettingsMigrator.CurrentSchemaVersion}.");
        }

        var resetFields = SettingsValidator.ValidateAndRepair(loaded);
        foreach (var field in resetFields)
        {
            notices.Add($"Reset invalid field '{field}' to its default.");
        }

        lock (_gate)
        {
            _current = loaded;
        }

        if (migrated || resetFields.Count > 0)
        {
            await SaveAsync(loaded, cancellationToken).ConfigureAwait(false);
        }

        ApplyNotices(notices);
        _logger.LogInformation("Loaded settings from {SettingsPath}", SettingsPath);
        return Clone(loaded);
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        NormalizeNulls(settings);
        SettingsValidator.ValidateAndRepair(settings);

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
            File.Copy(SettingsPath, _paths.SettingsBackupFilePath, overwrite: true);
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

    /// <summary>Exports settings JSON (no secrets are stored in settings.json).</summary>
    public async Task ExportAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        var json = JsonSerializer.Serialize(Current, SerializerOptions);
        await File.WriteAllTextAsync(destinationPath, json, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Exported settings to {Destination}", destinationPath);
    }

    public async Task ImportAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Import file not found.", sourcePath);
        }

        var imported = await ReadSettingsFileAsync(sourcePath, cancellationToken).ConfigureAwait(false)
                       ?? AppSettings.CreateDefault();
        NormalizeNulls(imported);
        SettingsMigrator.Migrate(imported);
        SettingsValidator.ValidateAndRepair(imported);
        await SaveAsync(imported, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Imported settings from {Source}", sourcePath);
    }

    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        await SaveAsync(AppSettings.CreateDefault(), cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Reset settings to defaults");
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

            var loaded = await ReadSettingsFileAsync(SettingsPath, CancellationToken.None).ConfigureAwait(false);
            if (loaded is null)
            {
                return;
            }

            NormalizeNulls(loaded);
            SettingsMigrator.Migrate(loaded);
            SettingsValidator.ValidateAndRepair(loaded);
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

    private void ApplyNotices(List<string> notices)
    {
        lock (_gate)
        {
            _lastLoadNotices = notices;
        }
    }

    private bool TryRestoreFromBackup(List<string> notices)
    {
        if (!File.Exists(_paths.SettingsBackupFilePath))
        {
            return false;
        }

        try
        {
            File.Copy(_paths.SettingsBackupFilePath, SettingsPath, overwrite: true);
            notices.Add("Restored settings from settings.backup.json.");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Failed to restore settings from backup");
            return false;
        }
    }

    private static async Task<AppSettings?> ReadSettingsFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return await JsonSerializer.DeserializeAsync<AppSettings>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    private static void NormalizeNulls(AppSettings settings)
    {
        settings.Hotkeys ??= new HotkeySettings();
        settings.Suggestions ??= new SuggestionSettings();
        settings.Grammar ??= new GrammarSettings();
        settings.Llm ??= new LlmSettings();
        settings.Button ??= new ButtonSettings();
        settings.SmartLists ??= new SmartListsSettings();
        settings.PromptAssist ??= new PromptAssistSettings();
        settings.Snippets ??= new SnippetsSettings();
        settings.Speech ??= new SpeechSettings();
        settings.TranslateTargets ??= ["English", "Arabic"];
        settings.BlockedProcesses ??= AppSettings.DefaultBlockedProcesses();
        settings.Grammar.DisabledRules ??= [];
        settings.SmartLists.DisabledProcesses ??= AppSettings.DefaultSmartListDisabledProcesses();
        settings.PromptAssist.Targets ??= AppSettings.DefaultPromptAssistTargets();
        if (string.IsNullOrWhiteSpace(settings.UiLanguage))
        {
            settings.UiLanguage = "auto";
        }
    }

    private static AppSettings Clone(AppSettings source)
    {
        var json = JsonSerializer.Serialize(source, SerializerOptions);
        return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? AppSettings.CreateDefault();
    }
}
