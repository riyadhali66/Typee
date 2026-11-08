namespace Typee.Core.Text;

public static class TextSegmenter
{
    private static readonly char[] SentenceTerminators = ['.', '!', '?', '。', '！', '？'];

    public static int FindParagraphStart(string text, int caretOffset)
    {
        if (string.IsNullOrEmpty(text) || caretOffset <= 0)
        {
            return 0;
        }

        var index = Math.Clamp(caretOffset - 1, 0, text.Length - 1);
        var paragraphBreak = text.LastIndexOfAny(['\r', '\n'], index);
        return paragraphBreak < 0 ? 0 : paragraphBreak + (text[paragraphBreak] == '\r' && paragraphBreak + 1 < text.Length && text[paragraphBreak + 1] == '\n' ? 2 : 1);
    }

    public static int FindParagraphEnd(string text, int caretOffset)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var index = Math.Clamp(caretOffset, 0, text.Length);
        for (var i = index; i < text.Length; i++)
        {
            if (text[i] is '\r' or '\n')
            {
                return i;
            }
        }

        return text.Length;
    }

    public static string GetParagraphAt(string text, int caretOffset)
    {
        var start = FindParagraphStart(text, caretOffset);
        var end = FindParagraphEnd(text, caretOffset);
        return text[start..end];
    }

    public static int FindLineStart(string text, int caretOffset)
    {
        if (string.IsNullOrEmpty(text) || caretOffset <= 0)
        {
            return 0;
        }

        var index = Math.Clamp(caretOffset - 1, 0, text.Length - 1);
        var lineBreak = text.LastIndexOfAny(['\r', '\n'], index);
        return lineBreak < 0 ? 0 : lineBreak + (text[lineBreak] == '\r' && lineBreak + 1 < text.Length && text[lineBreak + 1] == '\n' ? 2 : 1);
    }

    public static int FindLineEnd(string text, int caretOffset)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var index = Math.Clamp(caretOffset, 0, text.Length);
        for (var i = index; i < text.Length; i++)
        {
            if (text[i] is '\r' or '\n')
            {
                return i;
            }
        }

        return text.Length;
    }

    public static string GetLineAt(string text, int caretOffset)
    {
        var start = FindLineStart(text, caretOffset);
        var end = FindLineEnd(text, caretOffset);
        return text[start..end];
    }

    public static string? GetPreviousLine(string text, int caretOffset)
    {
        var lineStart = FindLineStart(text, caretOffset);
        if (lineStart <= 0)
        {
            return null;
        }

        var previousEnd = lineStart;
        while (previousEnd > 0 && text[previousEnd - 1] is '\r' or '\n')
        {
            previousEnd--;
        }

        return GetLineAt(text, previousEnd);
    }

    public static int FindSentenceStart(string text, int caretOffset)
    {
        if (string.IsNullOrEmpty(text) || caretOffset <= 0)
        {
            return 0;
        }

        var index = Math.Clamp(caretOffset - 1, 0, text.Length - 1);
        for (var i = index; i >= 0; i--)
        {
            if (SentenceTerminators.Contains(text[i]))
            {
                var start = i + 1;
                while (start < text.Length && char.IsWhiteSpace(text[start]))
                {
                    start++;
                }

                return start;
            }
        }

        return 0;
    }

    public static int FindSentenceEnd(string text, int caretOffset)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var index = Math.Clamp(caretOffset, 0, text.Length);
        for (var i = index; i < text.Length; i++)
        {
            if (SentenceTerminators.Contains(text[i]))
            {
                return i + 1;
            }

            if (text[i] is '\r' or '\n')
            {
                return i;
            }
        }

        return text.Length;
    }

    public static string GetSentenceAt(string text, int caretOffset)
    {
        var start = FindSentenceStart(text, caretOffset);
        var end = FindSentenceEnd(text, caretOffset);
        return text[start..end];
    }
}
