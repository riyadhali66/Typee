using Microsoft.Data.Sqlite;
using Typee.Core.Abstractions;

namespace Typee.Storage.Sqlite;

public sealed class SqliteDbConnectionFactory
{
    private readonly IAppPaths _paths;

    public SqliteDbConnectionFactory(IAppPaths paths)
    {
        _paths = paths;
    }

    public string DatabasePath => _paths.DatabaseFilePath;

    public SqliteConnection OpenConnection()
    {
        Directory.CreateDirectory(_paths.RootDirectory);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _paths.DatabaseFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = ON; PRAGMA journal_mode = WAL;";
        cmd.ExecuteNonQuery();
        return connection;
    }
}
