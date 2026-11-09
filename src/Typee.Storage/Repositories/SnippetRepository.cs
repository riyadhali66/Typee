using Dapper;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Storage.Sqlite;

namespace Typee.Storage.Repositories;

public sealed class SnippetRepository : ISnippetRepository
{
    private readonly SqliteDbConnectionFactory _factory;

    public SnippetRepository(SqliteDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<Snippet>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        var rows = await connection.QueryAsync<SnippetRow>(
            "SELECT id, trigger, body, use_count, updated_utc FROM snippets ORDER BY trigger COLLATE NOCASE;").ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    public async Task<Snippet> UpsertAsync(Snippet snippet, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        if (snippet.Id > 0)
        {
            await connection.ExecuteAsync(
                """
                UPDATE snippets
                SET trigger = @Trigger, body = @Body, use_count = @UseCount, updated_utc = @UpdatedUtc
                WHERE id = @Id;
                """,
                new
                {
                    snippet.Id,
                    snippet.Trigger,
                    snippet.Body,
                    snippet.UseCount,
                    UpdatedUtc = SqliteDate.Format(snippet.UpdatedUtc)
                }).ConfigureAwait(false);
            return snippet;
        }

        var id = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO snippets(trigger, body, use_count, updated_utc)
            VALUES (@Trigger, @Body, @UseCount, @UpdatedUtc);
            SELECT last_insert_rowid();
            """,
            new
            {
                snippet.Trigger,
                snippet.Body,
                snippet.UseCount,
                UpdatedUtc = SqliteDate.Format(snippet.UpdatedUtc)
            }).ConfigureAwait(false);

        return new Snippet
        {
            Id = id,
            Trigger = snippet.Trigger,
            Body = snippet.Body,
            UseCount = snippet.UseCount,
            UpdatedUtc = snippet.UpdatedUtc
        };
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync("DELETE FROM snippets WHERE id = @Id;", new { Id = id }).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync("DELETE FROM snippets;").ConfigureAwait(false);
    }

    private static Snippet Map(SnippetRow row) => new()
    {
        Id = row.id,
        Trigger = row.trigger,
        Body = row.body,
        UseCount = row.use_count,
        UpdatedUtc = SqliteDate.Parse(row.updated_utc)
    };

    private sealed class SnippetRow
    {
        public long id { get; init; }
        public string trigger { get; init; } = "";
        public string body { get; init; } = "";
        public int use_count { get; init; }
        public string updated_utc { get; init; } = "";
    }
}
