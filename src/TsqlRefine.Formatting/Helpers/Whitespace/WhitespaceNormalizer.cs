using System.Text;
using TsqlRefine.Formatting.Helpers.Protection;

namespace TsqlRefine.Formatting.Helpers.Whitespace;

/// <summary>
/// Normalizes whitespace (indentation, line breaks, trailing whitespace) while preserving
/// protected regions (strings, comments, brackets).
/// </summary>
public static class WhitespaceNormalizer
{
    /// <summary>
    /// Maximum number of columns to cache for space-based indentation.
    /// </summary>
    private const int MaxCachedSpaceColumns = 32;

    /// <summary>
    /// Maximum number of tabs to cache for tab-based indentation.
    /// </summary>
    private const int MaxCachedTabs = 8;

    /// <summary>
    /// Cache of space-based indent strings (0 to MaxCachedSpaceColumns spaces).
    /// </summary>
    private static readonly string[] SpaceIndentCache = BuildSpaceIndentCache();

    /// <summary>
    /// Cache of tab-based indent strings (0 to MaxCachedTabs tabs).
    /// </summary>
    private static readonly string[] TabIndentCache = BuildTabIndentCache();

    private static string[] BuildSpaceIndentCache()
    {
        var cache = new string[MaxCachedSpaceColumns + 1];
        for (var i = 0; i <= MaxCachedSpaceColumns; i++)
        {
            cache[i] = new string(' ', i);
        }

        return cache;
    }

    private static string[] BuildTabIndentCache()
    {
        var cache = new string[MaxCachedTabs + 1];
        for (var i = 0; i <= MaxCachedTabs; i++)
        {
            cache[i] = new string('\t', i);
        }

        return cache;
    }

    /// <summary>
    /// Normalizes whitespace in SQL text according to formatting options.
    /// </summary>
    /// <param name="input">SQL text to normalize</param>
    /// <param name="options">Formatting options controlling whitespace normalization</param>
    /// <returns>SQL text with normalized whitespace</returns>
    public static string Normalize(string input, FormattingOptions options)
    {
        // Resolve output line ending based on setting and input detection
        var lineEnding = ResolveLineEnding(input, options.LineEnding);

        var sb = new StringBuilder(input.Length + 16);
        var line = new StringBuilder();
        var tracker = new ProtectedRegionTracker();

        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (TryConsumeNewline(input, ref i, c))
            {
                AppendProcessedLine(sb, line, options, tracker);
                sb.Append(lineEnding);
                line.Clear();
                continue;
            }

            line.Append(c);
        }

        if (line.Length > 0)
        {
            AppendProcessedLine(sb, line, options, tracker);
        }

        // Apply final newline option using the resolved line ending
        if (options.InsertFinalNewline && !EndsWithLineEnding(sb, lineEnding))
        {
            sb.Append(lineEnding);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Resolves the output line ending based on the setting and input content.
    /// </summary>
    internal static string ResolveLineEnding(string input, LineEnding setting)
    {
        return setting switch
        {
            LineEnding.CrLf => "\r\n",
            LineEnding.Lf => "\n",
            // CR-only is rare; default to CRLF (Windows-preferred)
            LineEnding.Auto => LineEndingHelpers.DetectLineEnding(input, "\r\n"),
            _ => "\r\n"
        };
    }


    private static bool EndsWithLineEnding(StringBuilder sb, string lineEnding)
    {
        if (sb.Length < lineEnding.Length)
        {
            return false;
        }

        var startIndex = sb.Length - lineEnding.Length;
        for (var i = 0; i < lineEnding.Length; i++)
        {
            if (sb[startIndex + i] != lineEnding[i])
            {
                return false;
            }
        }

        return true;
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

        // Whitespace-only lines become empty lines (unless inside protected region)
        if (!lineStartsInProtected && leadingLength == text.Length)
        {
            return;
        }

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

            // Use cache for common cases
            if (spaces == 0 && tabs <= MaxCachedTabs)
            {
                return TabIndentCache[tabs];
            }

            // Fall back to string creation for larger or mixed indents
            var tabPart = tabs <= MaxCachedTabs ? TabIndentCache[tabs] : new string('\t', tabs);
            var spacePart = spaces <= MaxCachedSpaceColumns ? SpaceIndentCache[spaces] : new string(' ', spaces);
            return tabPart + spacePart;
        }

        // Use cache for common space indents
        return columns <= MaxCachedSpaceColumns
            ? SpaceIndentCache[columns]
            : new string(' ', columns);
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
