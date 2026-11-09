using Microsoft.Extensions.Logging.Abstractions;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Core.Phrases;
using Typee.Storage.Repositories;
using Typee.Storage.Sqlite;

namespace Typee.Storage.Tests;

public sealed class SqliteStorageTests
{
    [Fact]
    public async Task Migrations_CreateAllTables_AndAreIdempotent()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var versions = await harness.Migrations.GetAppliedVersionsAsync();
        Assert.Contains(1, versions);

        var again = await harness.Migrations.ApplyPendingAsync();
        Assert.Equal(0, again);

        await using var connection = harness.Factory.OpenConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            SELECT name FROM sqlite_master
            WHERE type = 'table'
            ORDER BY name;
            """;
        var tables = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        Assert.Contains("phrases", tables);
        Assert.Contains("completion_cache", tables);
        Assert.Contains("user_dictionary", tables);
        Assert.Contains("snippets", tables);
        Assert.Contains("custom_commands", tables);
        Assert.Contains("recent_instructions", tables);
        Assert.Contains("action_profile_memory", tables);
        Assert.Contains("endpoint_acknowledgments", tables);
        Assert.Contains("caret_method_memory", tables);
        Assert.Contains("schema_migrations", tables);
    }

    [Fact]
    public async Task PhraseRepository_RoundTripsAndFindsByText()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var repo = new PhraseRepository(harness.Factory);

        var inserted = await repo.InsertAsync(new Phrase
        {
            Text = "hello world",
            TriggerPrefix = "hello world",
            UseCount = 1,
            LastUsedUtc = DateTimeOffset.Parse("2026-03-01T12:00:00Z"),
            Source = PhraseSource.Manual
        });

        Assert.True(inserted.Id > 0);
        var found = await repo.FindByTextAsync("HELLO WORLD");
        Assert.NotNull(found);
        Assert.Equal(inserted.Id, found.Id);
        Assert.Equal(PhraseSource.Manual, found.Source);
    }

    [Fact]
    public async Task CompletionCache_HonorsTtlDelete()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var repo = new CompletionCacheRepository(harness.Factory);
        var now = DateTimeOffset.Parse("2026-03-10T00:00:00Z");

        await repo.UpsertAsync(new CompletionCacheEntry
        {
            ContextHash = "abc",
            Suggestion = "cached",
            Level = SuggestionLevel.Completion,
            CreatedUtc = now.AddHours(-48)
        });

        await repo.DeleteExpiredAsync(TimeSpan.FromHours(24), now);
        Assert.Null(await repo.TryGetAsync("abc"));
    }

    [Fact]
    public async Task RecentInstructions_KeepsOnlyLastTen()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var repo = new RecentInstructionRepository(harness.Factory);
        var start = DateTimeOffset.Parse("2026-03-01T00:00:00Z");

        for (var i = 0; i < 12; i++)
        {
            await repo.AddAsync($"instruction-{i}", start.AddMinutes(i));
        }

        var recent = await repo.GetRecentAsync();
        Assert.Equal(10, recent.Count);
        Assert.Equal("instruction-11", recent[0].Text);
        Assert.DoesNotContain(recent, r => r.Text == "instruction-0");
        Assert.DoesNotContain(recent, r => r.Text == "instruction-1");
    }

    [Fact]
    public async Task CaretMethodMemory_StoresProcessNameAndMethodOnly()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var repo = new CaretMethodMemoryRepository(harness.Factory);
        repo.Upsert("notepad.exe", CaretDetectionMethod.TextPattern2CaretRange, DateTimeOffset.UtcNow);

        Assert.Equal(CaretDetectionMethod.TextPattern2CaretRange, repo.TryGet("NOTEPAD.EXE"));
    }

    [Fact]
    public async Task TypeeDatabase_ClearLearned_RetainsSnippets()
    {
        await using var harness = await SqliteTestHarness.CreateAsync();
        var phrases = new PhraseRepository(harness.Factory);
        var snippets = new SnippetRepository(harness.Factory);
        var dictionary = new UserDictionaryRepository(harness.Factory);
        var cache = new CompletionCacheRepository(harness.Factory);
        var recent = new RecentInstructionRepository(harness.Factory);
        var profiles = new ActionProfileMemoryRepository(harness.Factory);
        var acks = new EndpointAcknowledgmentRepository(harness.Factory);
        var caret = new CaretMethodMemoryRepository(harness.Factory);
        var phraseService = new PhraseService(phrases, NullLogger<PhraseService>.Instance);
        var db = new TypeeDatabase(
            harness.Factory,
            harness.Migrations,
            phrases,
            cache,
            dictionary,
            recent,
            profiles,
            acks,
            caret,
            phraseService,
            NullLogger<TypeeDatabase>.Instance);

        await db.InitializeAsync();
        await phrases.InsertAsync(new Phrase
        {
            Text = "learned phrase",
            TriggerPrefix = "learned",
            UseCount = 1,
            Source = PhraseSource.Accepted
        });
        await snippets.UpsertAsync(new Snippet
        {
            Trigger = ";sig",
            Body = "Best regards",
            UseCount = 0,
            UpdatedUtc = DateTimeOffset.UtcNow
        });

        await db.ClearLearnedDataAsync();

        Assert.Empty(await phrases.GetAllAsync());
        Assert.Single(await snippets.GetAllAsync());
    }

    private sealed class SqliteTestHarness : IAsyncDisposable
    {
        private readonly string _root;

        private SqliteTestHarness(string root, SqliteDbConnectionFactory factory, MigrationRunner migrations)
        {
            _root = root;
            Factory = factory;
            Migrations = migrations;
        }

        public SqliteDbConnectionFactory Factory { get; }
        public MigrationRunner Migrations { get; }

        public static async Task<SqliteTestHarness> CreateAsync()
        {
            var root = Path.Combine(Path.GetTempPath(), "TypeeTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var paths = new TypeePathsForTests(root);
            var factory = new SqliteDbConnectionFactory(paths);
            var migrations = new MigrationRunner(factory, NullLogger<MigrationRunner>.Instance);
            await migrations.ApplyPendingAsync();
            return new SqliteTestHarness(root, factory, migrations);
        }

        public ValueTask DisposeAsync()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class TypeePathsForTests : IAppPaths
    {
        public TypeePathsForTests(string root)
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
