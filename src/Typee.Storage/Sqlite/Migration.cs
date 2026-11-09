namespace Typee.Storage.Sqlite;

public sealed record Migration(int Version, string Name, string Sql);
