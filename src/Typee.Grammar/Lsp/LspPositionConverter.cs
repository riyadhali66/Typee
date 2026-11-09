namespace Typee.Grammar.Lsp;

/// <summary>
/// Converts between .NET string offsets (UTF-16 code units) and LSP positions
/// (zero-based line + UTF-16 character). Surrogate pairs count as two characters,
/// matching the LSP UTF-16 position encoding.
/// </summary>
public static class LspPositionConverter
{
    public static (int Line, int Character) OffsetToPosition(string text, int offset)
    {
        ArgumentNullException.ThrowIfNull(text);
        offset = Math.Clamp(offset, 0, text.Length);

        var line = 0;
        var character = 0;
        var i = 0;
        while (i < offset)
        {
            var c = text[i];
            if (c == '\r')
            {
                i++;
                if (i < text.Length && text[i] == '\n')
                {
                    i++;
                }

                line++;
                character = 0;
            }
            else if (c == '\n')
            {
                i++;
                line++;
                character = 0;
            }
            else
            {
                i++;
                character++;
            }
        }

        return (line, character);
    }

    public static int PositionToOffset(string text, int line, int character)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (line < 0 || character < 0)
        {
            return 0;
        }

        var i = 0;
        var currentLine = 0;
        while (i < text.Length && currentLine < line)
        {
            var c = text[i];
            if (c == '\r')
            {
                i++;
                if (i < text.Length && text[i] == '\n')
                {
                    i++;
                }

                currentLine++;
            }
            else if (c == '\n')
            {
                i++;
                currentLine++;
            }
            else
            {
                i++;
            }
        }

        if (currentLine < line)
        {
            return text.Length;
        }

        var col = 0;
        while (i < text.Length && col < character)
        {
            if (text[i] is '\r' or '\n')
            {
                break;
            }

            i++;
            col++;
        }

        return i;
    }
}
