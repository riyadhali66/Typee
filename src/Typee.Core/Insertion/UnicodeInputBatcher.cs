namespace Typee.Core.Insertion;

/// <summary>
/// Builds UTF-16 code units for KEYEVENTF_UNICODE SendInput.
/// Surrogate pairs (emoji / non-BMP) stay as two consecutive units so the host receives a valid pair.
/// </summary>
public static class UnicodeInputBatcher
{
    public const int DefaultBatchSize = 64;

    /// <summary>
    /// Enumerates UTF-16 code units in order. Lone surrogates are passed through unchanged
    /// (host apps decide how to handle invalid sequences).
    /// </summary>
    public static IReadOnlyList<char> ToCodeUnits(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<char>();
        }

        return text.ToCharArray();
    }

    /// <summary>
    /// Splits code units into batches of at most <paramref name="batchSize"/> characters.
    /// Each batch becomes one SendInput call (KEYDOWN+KEYUP per unit → 2 INPUT structs each).
    /// </summary>
    public static IReadOnlyList<ArraySegment<char>> SplitBatches(IReadOnlyList<char> codeUnits, int batchSize = DefaultBatchSize)
    {
        if (codeUnits.Count == 0)
        {
            return Array.Empty<ArraySegment<char>>();
        }

        if (batchSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        }

        // Avoid splitting a surrogate pair across batches.
        var result = new List<ArraySegment<char>>();
        var buffer = codeUnits is char[] arr ? arr : codeUnits.ToArray();
        var offset = 0;
        while (offset < buffer.Length)
        {
            var take = Math.Min(batchSize, buffer.Length - offset);
            if (take > 0
                && offset + take < buffer.Length
                && char.IsHighSurrogate(buffer[offset + take - 1])
                && char.IsLowSurrogate(buffer[offset + take]))
            {
                take--;
            }

            if (take <= 0)
            {
                take = 1;
            }

            result.Add(new ArraySegment<char>(buffer, offset, take));
            offset += take;
        }

        return result;
    }

    public static int CountInputEvents(int codeUnitCount) => checked(codeUnitCount * 2);

    public static bool IsSurrogatePairAt(IReadOnlyList<char> units, int index)
        => index >= 0
           && index + 1 < units.Count
           && char.IsHighSurrogate(units[index])
           && char.IsLowSurrogate(units[index + 1]);
}
