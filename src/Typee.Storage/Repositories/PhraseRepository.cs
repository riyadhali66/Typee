using Dapper;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Storage.Sqlite;

namespace Typee.Storage.Repositories;

public sealed class PhraseRepository : IPhraseRepository
{
    private readonly SqliteDbConnectionFactory _factory;

    public PhraseRepository(SqliteDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<Phrase>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        var rows = await connection.QueryAsync<PhraseRow>(
            "SELECT id, text, trigger_prefix, use_count, last_used_utc, source FROM phrases ORDER BY id;").ConfigureAwait(false);
        return rows.Select(Map).ToList();
    }

    public async Task<Phrase?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        var row = await connection.QuerySingleOrDefaultAsync<PhraseRow>(
            "SELECT id, text, trigger_prefix, use_count, last_used_utc, source FROM phrases WHERE id = @Id;",
            new { Id = id }).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    public async Task<Phrase?> FindByTextAsync(string text, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        var row = await connection.QuerySingleOrDefaultAsync<PhraseRow>(
            "SELECT id, text, trigger_prefix, use_count, last_used_utc, source FROM phrases WHERE text = @Text COLLATE NOCASE;",
            new { Text = text }).ConfigureAwait(false);
        return row is null ? null : Map(row);
    }

    public async Task<Phrase> InsertAsync(Phrase phrase, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        var id = await connection.ExecuteScalarAsync<long>(
            """
            INSERT INTO phrases(text, trigger_prefix, use_count, last_used_utc, source)
            VALUES (@Text, @TriggerPrefix, @UseCount, @LastUsedUtc, @Source);
            SELECT last_insert_rowid();
            """,
            new
            {
                phrase.Text,
                phrase.TriggerPrefix,
                phrase.UseCount,
                LastUsedUtc = phrase.LastUsedUtc is { } t ? SqliteDate.Format(t) : null,
                Source = ToSource(phrase.Source)
            }).ConfigureAwait(false);

        return new Phrase
        {
            Id = id,
            Text = phrase.Text,
            TriggerPrefix = phrase.TriggerPrefix,
            UseCount = phrase.UseCount,
            LastUsedUtc = phrase.LastUsedUtc,
            Source = phrase.Source
        };
    }

    public async Task UpdateAsync(Phrase phrase, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync(
            """
            UPDATE phrases
            SET text = @Text,
                trigger_prefix = @TriggerPrefix,
                use_count = @UseCount,
                last_used_utc = @LastUsedUtc,
                source = @Source
            WHERE id = @Id;
            """,
            new
            {
                phrase.Id,
                phrase.Text,
                phrase.TriggerPrefix,
                phrase.UseCount,
                LastUsedUtc = phrase.LastUsedUtc is { } t ? SqliteDate.Format(t) : null,
                Source = ToSource(phrase.Source)
            }).ConfigureAwait(false);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync("DELETE FROM phrases WHERE id = @Id;", new { Id = id }).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync("DELETE FROM phrases;").ConfigureAwait(false);
    }

    private static Phrase Map(PhraseRow row) => new()
    {
        Id = row.id,
        Text = row.text,
        TriggerPrefix = row.trigger_prefix,
        UseCount = row.use_count,
        LastUsedUtc = SqliteDate.ParseNullable(row.last_used_utc),
        Source = FromSource(row.source)
    };

    private static string ToSource(PhraseSource source) => source switch
    {
        PhraseSource.Accepted => "accepted",
        PhraseSource.Manual => "manual",
        _ => "manual"
    };

    private static PhraseSource FromSource(string source) =>
        string.Equals(source, "accepted", StringComparison.OrdinalIgnoreCase)
            ? PhraseSource.Accepted
            : PhraseSource.Manual;

    private sealed class PhraseRow
    {
        public long id { get; init; }
        public string text { get; init; } = "";
        public string trigger_prefix { get; init; } = "";
        public int use_count { get; init; }
        public string? last_used_utc { get; init; }
        public string source { get; init; } = "manual";
    }
}
