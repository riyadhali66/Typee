using Dapper;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Storage.Sqlite;

namespace Typee.Storage.Repositories;

public sealed class EndpointAcknowledgmentRepository : IEndpointAcknowledgmentRepository
{
    private readonly SqliteDbConnectionFactory _factory;

    public EndpointAcknowledgmentRepository(SqliteDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<bool> IsAcknowledgedAsync(string profileName, string host, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        var count = await connection.ExecuteScalarAsync<long>(
            """
            SELECT COUNT(1) FROM endpoint_acknowledgments
            WHERE profile_name = @ProfileName AND host = @Host COLLATE NOCASE;
            """,
            new { ProfileName = profileName, Host = host }).ConfigureAwait(false);
        return count > 0;
    }

    public async Task AcknowledgeAsync(string profileName, string host, DateTimeOffset acknowledgedUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync(
            """
            INSERT INTO endpoint_acknowledgments(profile_name, host, acknowledged_utc)
            VALUES (@ProfileName, @Host, @AcknowledgedUtc)
            ON CONFLICT(profile_name, host) DO UPDATE SET
              acknowledged_utc = excluded.acknowledged_utc;
            """,
            new
            {
                ProfileName = profileName,
                Host = host,
                AcknowledgedUtc = SqliteDate.Format(acknowledgedUtc)
            }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EndpointAcknowledgment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        var rows = await connection.QueryAsync<AckRow>(
            "SELECT profile_name, host, acknowledged_utc FROM endpoint_acknowledgments;").ConfigureAwait(false);
        return rows.Select(static r => new EndpointAcknowledgment
        {
            ProfileName = r.profile_name,
            Host = r.host,
            AcknowledgedUtc = SqliteDate.Parse(r.acknowledged_utc)
        }).ToList();
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync("DELETE FROM endpoint_acknowledgments;").ConfigureAwait(false);
    }

    private sealed class AckRow
    {
        public string profile_name { get; init; } = "";
        public string host { get; init; } = "";
        public string acknowledged_utc { get; init; } = "";
    }
}
