namespace Typee.Grammar;

/// <summary>
/// Helpers for skipping non-Latin paragraphs (Harper is English-only) and
/// locating the word currently being typed.
/// Callers: GrammarService.CheckAsync / FilterIssues. No prior equivalent.
/// User: "skip paragraphs that are mostly non-Latin" / "Ignore issues on the word currently being typed."
/// </summary>
public static class ScriptHeuristics
{
    /// <summary>
    /// Returns true when letter characters are mostly Latin (Basic Latin + Latin-1 + Latin Extended).
    /// Empty / no-letter text is treated as Latin-safe (allow check).
    /// </summary>
    public static bool IsMostlyLatin(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var letters = 0;
        var latin = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (!RuneIsLetter(rune.Value))
            {
                continue;
            }

            letters++;
            if (IsLatinLetter(rune.Value))
            {
                latin++;
            }
        }

        if (letters == 0)
        {
            return true;
        }

        return latin * 100 / letters >= 60;
    }

    public static (int Start, int Length) GetWordSpanAtCaret(string text, int caretOffset)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (0, 0);
        }

        var caret = Math.Clamp(caretOffset, 0, text.Length);
        var start = caret;
        while (start > 0 && IsWordChar(text[start - 1]))
        {
            start--;
        }

        var end = caret;
        while (end < text.Length && IsWordChar(text[end]))
        {
            end++;
        }

        return (start, end - start);
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '\'' or '\u2019' or '-';

    private static bool RuneIsLetter(int value) =>
        char.IsLetter(char.ConvertFromUtf32(value), 0);

    private static bool IsLatinLetter(int value) =>
        (value >= 'A' && value <= 'Z')
        || (value >= 'a' && value <= 'z')
        || (value >= 0x00C0 && value <= 0x024F)
        || (value >= 0x1E00 && value <= 0x1EFF);
}
