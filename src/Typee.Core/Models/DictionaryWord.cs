namespace Typee.Core.Models;

public sealed class DictionaryWord
{
    public required string Word { get; init; }
    public DateTimeOffset AddedUtc { get; init; }
}
