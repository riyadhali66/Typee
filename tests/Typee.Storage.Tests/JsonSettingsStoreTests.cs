using Microsoft.Extensions.Logging.Abstractions;
using Typee.Core.Abstractions;
using Typee.Core.Settings;
using Typee.Storage.Settings;

namespace Typee.Storage.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "TypeeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = new TestPaths(root);
            using var store = new JsonSettingsStore(paths, NullLogger<JsonSettingsStore>.Instance);

            var settings = AppSettings.CreateDefault();
            settings.Suggestions.DebounceMs = 750;
            settings.Llm.Model = "test-model";
            settings.BlockedProcesses = ["notepad.exe"];

            await store.SaveAsync(settings);
            var loaded = await store.LoadAsync();

            Assert.Equal(750, loaded.Suggestions.DebounceMs);
            Assert.Equal("test-model", loaded.Llm.Model);
            Assert.Equal(["notepad.exe"], loaded.BlockedProcesses);
            Assert.True(File.Exists(paths.SettingsFilePath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SaveAsync_WritesBackupCopy()
    {
        var root = Path.Combine(Path.GetTempPath(), "TypeeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = new TestPaths(root);
            using var store = new JsonSettingsStore(paths, NullLogger<JsonSettingsStore>.Instance);
            await store.SaveAsync(AppSettings.CreateDefault());
            Assert.True(File.Exists(paths.SettingsBackupFilePath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_CreatesDefaultsWhenMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "TypeeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var paths = new TestPaths(root);
            using var store = new JsonSettingsStore(paths, NullLogger<JsonSettingsStore>.Instance);

            var loaded = await store.LoadAsync();

            Assert.True(File.Exists(paths.SettingsFilePath));
            Assert.Equal("Ctrl+Shift+Space", loaded.Hotkeys.CommandBar);
            Assert.Equal("TYPEE_API_KEY", loaded.Llm.ApiKeyEnvVar);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class TestPaths : IAppPaths
    {
        public TestPaths(string root)
        {
            RootDirectory = root;
            SettingsFilePath = Path.Combine(root, "settings.json");
            SettingsBackupFilePath = Path.Combine(root, "settings.backup.json");
            CrashMarkerFilePath = Path.Combine(root, "crash.marker");
            LogsDirectory = Path.Combine(root, "logs");
            DatabaseFilePath = Path.Combine(root, "typee.db");
            ModelsDirectory = Path.Combine(root, "models");
        }

        public string RootDirectory { get; }
        public string SettingsFilePath { get; }
        public string SettingsBackupFilePath { get; }
        public string CrashMarkerFilePath { get; }
        public string LogsDirectory { get; }
        public string DatabaseFilePath { get; }
        public string ModelsDirectory { get; }
    }
}
