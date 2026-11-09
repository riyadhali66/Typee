namespace Typee.Storage.Sqlite;

internal static class SqliteDate
{
    public static string Format(DateTimeOffset value) => value.ToUniversalTime().ToString("O");

    public static DateTimeOffset? ParseNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTimeOffset.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
    }

    public static DateTimeOffset Parse(string value) =>
        DateTimeOffset.Parse(value, null, System.Globalization.DateTimeStyles.RoundtripKind);
}
