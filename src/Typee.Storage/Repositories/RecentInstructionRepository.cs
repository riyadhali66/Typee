using Dapper;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Storage.Sqlite;

namespace Typee.Storage.Repositories;

public sealed class RecentInstructionRepository : IRecentInstructionRepository
{
    public const int MaxRecent = 10;

    private readonly SqliteDbConnectionFactory _factory;

    public RecentInstructionRepository(SqliteDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<RecentInstruction>> GetRecentAsync(int limit = MaxRecent, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, MaxRecent);
        await using var connection = _factory.OpenConnection();
        var rows = await connection.QueryAsync<RecentRow>(
            """
            SELECT id, text, used_utc
            FROM recent_instructions
            ORDER BY used_utc DESC, id DESC
            LIMIT @Limit;
            """,
            new { Limit = take }).ConfigureAwait(false);
        return rows.Select(static r => new RecentInstruction
        {
            Id = r.id,
            Text = r.text,
            UsedUtc = SqliteDate.Parse(r.used_utc)
        }).ToList();
    }

    public async Task AddAsync(string text, DateTimeOffset usedUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        await using var connection = _factory.OpenConnection();
        await using var tx = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await connection.ExecuteAsync(
            "INSERT INTO recent_instructions(text, used_utc) VALUES (@Text, @UsedUtc);",
            new { Text = text.Trim(), UsedUtc = SqliteDate.Format(usedUtc) },
            tx).ConfigureAwait(false);

        await connection.ExecuteAsync(
            """
            DELETE FROM recent_instructions
            WHERE id NOT IN (
              SELECT id FROM recent_instructions
              ORDER BY used_utc DESC, id DESC
              LIMIT @Limit
            );
            """,
            new { Limit = MaxRecent },
            tx).ConfigureAwait(false);

        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync("DELETE FROM recent_instructions;").ConfigureAwait(false);
    }

    private sealed class RecentRow
    {
        public long id { get; init; }
        public string text { get; init; } = "";
        public string used_utc { get; init; } = "";
    }
}
