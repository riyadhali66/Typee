using Dapper;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Storage.Sqlite;

namespace Typee.Storage.Repositories;

public sealed class CompletionCacheRepository : ICompletionCacheRepository
{
    private readonly SqliteDbConnectionFactory _factory;

    public CompletionCacheRepository(SqliteDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<CompletionCacheEntry?> TryGetAsync(string contextHash, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        var row = await connection.QuerySingleOrDefaultAsync<CacheRow>(
            "SELECT context_hash, suggestion, level, created_utc FROM completion_cache WHERE context_hash = @Hash;",
            new { Hash = contextHash }).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    public async Task UpsertAsync(CompletionCacheEntry entry, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync(
            """
            INSERT INTO completion_cache(context_hash, suggestion, level, created_utc)
            VALUES (@ContextHash, @Suggestion, @Level, @CreatedUtc)
            ON CONFLICT(context_hash) DO UPDATE SET
              suggestion = excluded.suggestion,
              level = excluded.level,
              created_utc = excluded.created_utc;
            """,
            new
            {
                entry.ContextHash,
                entry.Suggestion,
                Level = (int)entry.Level,
                CreatedUtc = SqliteDate.Format(entry.CreatedUtc)
            }).ConfigureAwait(false);
    }

    public async Task DeleteExpiredAsync(TimeSpan ttl, DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        var cutoff = SqliteDate.Format(utcNow - ttl);
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync(
            "DELETE FROM completion_cache WHERE created_utc < @Cutoff;",
            new { Cutoff = cutoff }).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync("DELETE FROM completion_cache;").ConfigureAwait(false);
    }

    private static CompletionCacheEntry Map(CacheRow row) => new()
    {
        ContextHash = row.context_hash,
        Suggestion = row.suggestion,
        Level = (SuggestionLevel)row.level,
        CreatedUtc = SqliteDate.Parse(row.created_utc)
    };

    private sealed class CacheRow
    {
        public string context_hash { get; init; } = "";
        public string suggestion { get; init; } = "";
        public int level { get; init; }
        public string created_utc { get; init; } = "";
    }
}
