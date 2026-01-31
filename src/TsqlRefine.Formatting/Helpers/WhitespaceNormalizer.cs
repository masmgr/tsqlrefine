using System.Text;

namespace TsqlRefine.Formatting.Helpers;

/// <summary>
/// Normalizes whitespace (indentation, line breaks, trailing whitespace) while preserving
/// protected regions (strings, comments, brackets).
/// </summary>
public static class WhitespaceNormalizer
{
    /// <summary>
    /// Normalizes whitespace in SQL text according to formatting options.
    /// </summary>
    /// <param name="input">SQL text to normalize</param>
    /// <param name="options">Formatting options controlling whitespace normalization</param>
    /// <returns>SQL text with normalized whitespace</returns>
    public static string Normalize(string input, FormattingOptions options)
    {
        var sb = new StringBuilder(input.Length + 16);
        var line = new StringBuilder();
        var tracker = new ProtectedRegionTracker();

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (TryConsumeNewline(input, ref i, c))
            {
                AppendProcessedLine(sb, line, options, tracker);
                sb.Append('\n');
                line.Clear();
                continue;
            }

            line.Append(c);
        }

        if (line.Length > 0)
        {
            AppendProcessedLine(sb, line, options, tracker);
        }

        var result = sb.ToString();

        // Apply final newline option
        if (options.InsertFinalNewline && !result.EndsWith('\n'))
        {
            result += '\n';
        }

        return result;
    }

    private static bool TryConsumeNewline(string input, ref int index, char current)
    {
        if (current is not ('\r' or '\n'))
        {
            return false;
        }

        if (current == '\r' && index + 1 < input.Length && input[index + 1] == '\n')
        {
            index++;
        }

        return true;
    }

    private static void AppendProcessedLine(
        StringBuilder output,
        StringBuilder line,
        FormattingOptions options,
        ProtectedRegionTracker tracker)
    {
        if (line.Length == 0)
        {
            return;
        }

        var text = line.ToString();
        var lineStartsInProtected = tracker.IsInProtectedRegion();
        var lineContainsProtected = lineStartsInProtected;

        var indentSize = GetIndentSize(options);
        GetLeadingWhitespace(text, indentSize, out var leadingLength, out var columns);

        var sbLine = new StringBuilder(text.Length);
        if (lineStartsInProtected)
        {
            sbLine.Append(text.AsSpan(0, leadingLength));
        }
        else
        {
            sbLine.Append(BuildIndent(columns, indentSize, options));
        }

        var i = leadingLength;
        var inLineComment = false;
        while (i < text.Length)
        {
            if (inLineComment)
            {
                sbLine.Append(text.AsSpan(i));
                break;
            }

            var c = text[i];

            if (tracker.TryConsume(text, sbLine, ref i))
            {
                continue;
            }

            if (ProtectedRegionTracker.TryStartLineComment(text, sbLine, ref i, ref inLineComment))
            {
                lineContainsProtected = true;
                continue;
            }

            if (tracker.TryStartProtectedRegion(text, sbLine, ref i))
            {
                lineContainsProtected = true;
                continue;
            }

            sbLine.Append(c);
            i++;
        }

        // Trim trailing whitespace only if we're not currently in a protected region
        // (e.g., inside a multi-line string or block comment that continues to next line).
        // Lines that contained protected regions (but ended outside them) can still be trimmed.
        if (options.TrimTrailingWhitespace && !tracker.IsInProtectedRegion())
        {
            TrimTrailingWhitespace(sbLine);
        }

        output.Append(sbLine);
    }

    private static void GetLeadingWhitespace(string text, int indentSize, out int leadingLength, out int columns)
    {
        leadingLength = 0;
        columns = 0;

        while (leadingLength < text.Length)
        {
            var c = text[leadingLength];
            if (c == ' ')
            {
                columns++;
                leadingLength++;
                continue;
            }

            if (c == '\t')
            {
                columns += indentSize;
                leadingLength++;
                continue;
            }

            break;
        }
    }

    private static int GetIndentSize(FormattingOptions options) =>
        options.IndentSize <= 0 ? 4 : options.IndentSize;

    private static string BuildIndent(int columns, int indentSize, FormattingOptions options)
    {
        if (columns <= 0)
        {
            return string.Empty;
        }

        if (options.IndentStyle == IndentStyle.Tabs)
        {
            var tabs = columns / indentSize;
            var spaces = columns % indentSize;
            return new string('\t', tabs) + new string(' ', spaces);
        }

        return new string(' ', columns);
    }

    private static void TrimTrailingWhitespace(StringBuilder sb)
    {
        var i = sb.Length - 1;
        while (i >= 0 && (sb[i] == ' ' || sb[i] == '\t'))
        {
            i--;
        }

        if (i < sb.Length - 1)
        {
            sb.Length = i + 1;
        }
    }
}
