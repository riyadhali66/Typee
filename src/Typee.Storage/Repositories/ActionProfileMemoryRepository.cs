using Dapper;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Storage.Sqlite;

namespace Typee.Storage.Repositories;

public sealed class ActionProfileMemoryRepository : IActionProfileMemoryRepository
{
    private readonly SqliteDbConnectionFactory _factory;

    public ActionProfileMemoryRepository(SqliteDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<ActionProfileMemory?> GetAsync(string actionId, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        var row = await connection.QuerySingleOrDefaultAsync<MemoryRow>(
            "SELECT action_id, profile_name, updated_utc FROM action_profile_memory WHERE action_id = @ActionId;",
            new { ActionId = actionId }).ConfigureAwait(false);
        return row is null
            ? null
            : new ActionProfileMemory
            {
                ActionId = row.action_id,
                ProfileName = row.profile_name,
                UpdatedUtc = SqliteDate.Parse(row.updated_utc)
            };
    }

    public async Task UpsertAsync(string actionId, string profileName, DateTimeOffset updatedUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync(
            """
            INSERT INTO action_profile_memory(action_id, profile_name, updated_utc)
            VALUES (@ActionId, @ProfileName, @UpdatedUtc)
            ON CONFLICT(action_id) DO UPDATE SET
              profile_name = excluded.profile_name,
              updated_utc = excluded.updated_utc;
            """,
            new
            {
                ActionId = actionId,
                ProfileName = profileName,
                UpdatedUtc = SqliteDate.Format(updatedUtc)
            }).ConfigureAwait(false);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync("DELETE FROM action_profile_memory;").ConfigureAwait(false);
    }

    private sealed class MemoryRow
    {
        public string action_id { get; init; } = "";
        public string profile_name { get; init; } = "";
        public string updated_utc { get; init; } = "";
    }
}
