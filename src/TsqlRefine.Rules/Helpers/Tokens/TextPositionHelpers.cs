using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Tokens;

/// <summary>
/// Utilities for converting between text offsets and line/column positions.
/// </summary>
public static class TextPositionHelpers
{
    /// <summary>
    /// Converts a character offset to a line/column position.
    /// </summary>
    /// <param name="text">The text to search within.</param>
    /// <param name="offset">The character offset (0-based).</param>
    /// <returns>A Position with line and character (both 0-based).</returns>
    public static Position OffsetToPosition(string text, int offset)
    {
        var line = 0;
        var character = 0;

        for (var i = 0; i < offset && i < text.Length; i++)
        {
            if (text[i] == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                line++;
                character = 0;
            }
            else if (text[i] == '\n')
            {
                line++;
                character = 0;
            }
            else
            {
                character++;
            }
        }

        return new Position(line, character);
    }

    /// <summary>
    /// Advances a position by the given text, accounting for \r\n, \r, and \n line breaks.
    /// This is the single source of truth for computing token end positions; use it
    /// instead of adding the text length to the start character, which is wrong for
    /// multi-line tokens such as block comments and multi-line string literals.
    /// </summary>
    /// <param name="start">The starting position.</param>
    /// <param name="text">The text consumed from the starting position.</param>
    /// <returns>The position immediately after the text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when start is null.</exception>
    public static Position AdvancePosition(Position start, string? text)
    {
        ArgumentNullException.ThrowIfNull(start);

        if (string.IsNullOrEmpty(text))
        {
            return start;
        }

        // Fast path: most tokens are single-line.
        if (text.AsSpan().IndexOfAny('\r', '\n') < 0)
        {
            return new Position(start.Line, start.Character + text.Length);
        }

        var line = start.Line;
        var character = start.Character;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                line++;
                character = 0;
                continue;
            }

            if (ch == '\n')
            {
                line++;
                character = 0;
                continue;
            }

            character++;
        }

        return new Position(line, character);
    }

    /// <summary>
    /// Gets the leading whitespace (indentation) of the line containing the given offset.
    /// </summary>
    /// <param name="text">The text to search within.</param>
    /// <param name="offset">The character offset within the line.</param>
    /// <returns>The indentation string (spaces and tabs only).</returns>
    public static string GetLineIndentation(string text, int offset)
    {
        // Find the start of the line containing this offset
        var lineStart = offset;
        while (lineStart > 0 && text[lineStart - 1] != '\n')
        {
            lineStart--;
        }

        // Extract leading whitespace
        var indent = new System.Text.StringBuilder();
        for (var i = lineStart; i < offset && i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == ' ' || ch == '\t')
            {
                indent.Append(ch);
            }
            else
            {
                break;
            }
        }

        return indent.ToString();
    }

    /// <summary>
    /// Detects the line ending style used in the text.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <returns>"\r\n" for Windows-style, "\n" for Unix-style.</returns>
    public static string DetectLineEnding(string text)
    {
        var crlfIndex = text.IndexOf("\r\n", StringComparison.Ordinal);
        var lfIndex = text.IndexOf('\n');

        if (crlfIndex >= 0 && (lfIndex < 0 || crlfIndex <= lfIndex))
        {
            return "\r\n";
        }

        return "\n";
    }
}
