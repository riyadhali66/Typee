using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Typee.Core.Abstractions;

namespace Typee.Storage.Sqlite;

public sealed class TypeeDatabase : ITypeeDatabase
{
    private readonly SqliteDbConnectionFactory _factory;
    private readonly MigrationRunner _migrations;
    private readonly IPhraseRepository _phrases;
    private readonly ICompletionCacheRepository _completionCache;
    private readonly IUserDictionaryRepository _dictionary;
    private readonly IRecentInstructionRepository _recentInstructions;
    private readonly IActionProfileMemoryRepository _actionProfiles;
    private readonly IEndpointAcknowledgmentRepository _endpointAcks;
    private readonly ICaretMethodMemoryRepository _caretMethods;
    private readonly IPhraseService _phraseService;
    private readonly ILogger<TypeeDatabase> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TypeeDatabase(
        SqliteDbConnectionFactory factory,
        MigrationRunner migrations,
        IPhraseRepository phrases,
        ICompletionCacheRepository completionCache,
        IUserDictionaryRepository dictionary,
        IRecentInstructionRepository recentInstructions,
        IActionProfileMemoryRepository actionProfiles,
        IEndpointAcknowledgmentRepository endpointAcks,
        ICaretMethodMemoryRepository caretMethods,
        IPhraseService phraseService,
        ILogger<TypeeDatabase> logger)
    {
        _factory = factory;
        _migrations = migrations;
        _phrases = phrases;
        _completionCache = completionCache;
        _dictionary = dictionary;
        _recentInstructions = recentInstructions;
        _actionProfiles = actionProfiles;
        _endpointAcks = endpointAcks;
        _caretMethods = caretMethods;
        _phraseService = phraseService;
        _logger = logger;
    }

    public string DatabasePath => _factory.DatabasePath;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var applied = await _migrations.ApplyPendingAsync(cancellationToken).ConfigureAwait(false);
            await _phraseService.InitializeAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "SQLite ready pathHash={PathHash} migrationsApplied={Count}",
                DatabasePath.GetHashCode(StringComparison.Ordinal),
                applied);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearLearnedDataAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _phrases.ClearAsync(cancellationToken).ConfigureAwait(false);
            await _completionCache.ClearAsync(cancellationToken).ConfigureAwait(false);
            await _dictionary.ClearAsync(cancellationToken).ConfigureAwait(false);
            await _recentInstructions.ClearAsync(cancellationToken).ConfigureAwait(false);
            await _actionProfiles.ClearAsync(cancellationToken).ConfigureAwait(false);
            await _endpointAcks.ClearAsync(cancellationToken).ConfigureAwait(false);
            await _caretMethods.ClearAsync(cancellationToken).ConfigureAwait(false);
            await _phraseService.ReloadAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Cleared learned SQLite tables (snippets/custom_commands retained)");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteDatabaseAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();

            var path = DatabasePath;
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                var side = path + suffix;
                if (File.Exists(side))
                {
                    File.Delete(side);
                }
            }

            await _migrations.ApplyPendingAsync(cancellationToken).ConfigureAwait(false);
            await _phraseService.ReloadAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Deleted and recreated typee.db");
        }
        finally
        {
            _gate.Release();
        }
    }
}
