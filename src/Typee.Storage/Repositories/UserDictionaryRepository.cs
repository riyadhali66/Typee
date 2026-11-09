using Dapper;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Storage.Sqlite;

namespace Typee.Storage.Repositories;

public sealed class UserDictionaryRepository : IUserDictionaryRepository
{
    private readonly SqliteDbConnectionFactory _factory;

    public UserDictionaryRepository(SqliteDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<DictionaryWord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        var rows = await connection.QueryAsync<DictRow>(
            "SELECT word, added_utc FROM user_dictionary ORDER BY word COLLATE NOCASE;").ConfigureAwait(false);
        return rows.Select(static r => new DictionaryWord
        {
            Word = r.word,
            AddedUtc = SqliteDate.Parse(r.added_utc)
        }).ToList();
    }

    public async Task UpsertAsync(string word, DateTimeOffset addedUtc, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync(
            """
            INSERT INTO user_dictionary(word, added_utc)
            VALUES (@Word, @AddedUtc)
            ON CONFLICT(word) DO UPDATE SET added_utc = excluded.added_utc;
            """,
            new { Word = word.Trim(), AddedUtc = SqliteDate.Format(addedUtc) }).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string word, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync(
            "DELETE FROM user_dictionary WHERE word = @Word COLLATE NOCASE;",
            new { Word = word }).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync("DELETE FROM user_dictionary;").ConfigureAwait(false);
    }

    private sealed class DictRow
    {
        public string word { get; init; } = "";
        public string added_utc { get; init; } = "";
    }
}
