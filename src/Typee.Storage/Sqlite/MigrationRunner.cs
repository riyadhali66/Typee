using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Typee.Storage.Sqlite;

public sealed class MigrationRunner
{
    private readonly SqliteDbConnectionFactory _factory;
    private readonly ILogger<MigrationRunner> _logger;

    public MigrationRunner(SqliteDbConnectionFactory factory, ILogger<MigrationRunner> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<int> ApplyPendingAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await EnsureMigrationsTableAsync(connection, cancellationToken).ConfigureAwait(false);
        var applied = await GetAppliedVersionsAsync(connection, cancellationToken).ConfigureAwait(false);
        var count = 0;

        foreach (var migration in SqliteMigrations.All.OrderBy(static m => m.Version))
        {
            if (applied.Contains(migration.Version))
            {
                continue;
            }

            await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = migration.Sql;
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = tx;
                    cmd.CommandText = "INSERT INTO schema_migrations(version, name, applied_utc) VALUES ($v, $n, $t);";
                    cmd.Parameters.AddWithValue("$v", migration.Version);
                    cmd.Parameters.AddWithValue("$n", migration.Name);
                    cmd.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToString("O"));
                    await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
                count++;
                _logger.LogInformation("Applied SQLite migration {Version} ({Name})", migration.Version, migration.Name);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken).ConfigureAwait(false);
                throw;
            }
        }

        return count;
    }

    public async Task<IReadOnlyList<int>> GetAppliedVersionsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await EnsureMigrationsTableAsync(connection, cancellationToken).ConfigureAwait(false);
        return await GetAppliedVersionsAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureMigrationsTableAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText =
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
              version INTEGER PRIMARY KEY,
              name TEXT NOT NULL,
              applied_utc TEXT NOT NULL
            );
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<int>> GetAppliedVersionsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT version FROM schema_migrations ORDER BY version;";
        var versions = new List<int>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            versions.Add(reader.GetInt32(0));
        }

        return versions;
    }
}
