using Dapper;
using Typee.Core.Abstractions;
using Typee.Core.Models;
using Typee.Storage.Sqlite;

namespace Typee.Storage.Repositories;

public sealed class CaretMethodMemoryRepository : ICaretMethodMemoryRepository
{
    private readonly SqliteDbConnectionFactory _factory;
    private readonly object _cacheGate = new();
    private readonly Dictionary<string, CaretDetectionMethod> _cache = new(StringComparer.OrdinalIgnoreCase);

    public CaretMethodMemoryRepository(SqliteDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public CaretDetectionMethod? TryGet(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return null;
        }

        lock (_cacheGate)
        {
            if (_cache.TryGetValue(processName, out var cached))
            {
                return cached;
            }
        }

        using var connection = _factory.OpenConnection();
        var methodName = connection.QuerySingleOrDefault<string>(
            "SELECT method FROM caret_method_memory WHERE process_name = @ProcessName COLLATE NOCASE;",
            new { ProcessName = processName });
        if (methodName is null || !Enum.TryParse<CaretDetectionMethod>(methodName, ignoreCase: true, out var method))
        {
            return null;
        }

        lock (_cacheGate)
        {
            _cache[processName] = method;
        }

        return method;
    }

    public Task<CaretDetectionMethod?> TryGetAsync(string processName, CancellationToken cancellationToken = default) =>
        Task.FromResult(TryGet(processName));

    public void Upsert(string processName, CaretDetectionMethod method, DateTimeOffset updatedUtc)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return;
        }

        using var connection = _factory.OpenConnection();
        connection.Execute(
            """
            INSERT INTO caret_method_memory(process_name, method, updated_utc)
            VALUES (@ProcessName, @Method, @UpdatedUtc)
            ON CONFLICT(process_name) DO UPDATE SET
              method = excluded.method,
              updated_utc = excluded.updated_utc;
            """,
            new
            {
                ProcessName = processName.Trim(),
                Method = method.ToString(),
                UpdatedUtc = SqliteDate.Format(updatedUtc)
            });

        lock (_cacheGate)
        {
            _cache[processName] = method;
        }
    }

    public Task UpsertAsync(string processName, CaretDetectionMethod method, DateTimeOffset updatedUtc, CancellationToken cancellationToken = default)
    {
        Upsert(processName, method, updatedUtc);
        return Task.CompletedTask;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _factory.OpenConnection();
        await connection.ExecuteAsync("DELETE FROM caret_method_memory;").ConfigureAwait(false);
        lock (_cacheGate)
        {
            _cache.Clear();
        }
    }
}
