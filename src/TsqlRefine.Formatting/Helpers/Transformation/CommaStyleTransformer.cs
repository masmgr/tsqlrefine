using System.Text;
using TsqlRefine.Formatting.Helpers.Protection;
using TsqlRefine.Formatting.Helpers.Whitespace;

namespace TsqlRefine.Formatting.Helpers.Transformation;

/// <summary>
/// Transforms SQL between trailing and leading comma styles.
/// </summary>
public static class CommaStyleTransformer
{
    /// <summary>
    /// Transforms trailing commas to leading commas.
    ///
    /// Example:
    /// Input:
    ///   SELECT id,
    ///          name,
    ///          email
    ///
    /// Output:
    ///   SELECT id
    ///        , name
    ///        , email
    ///
    /// Known limitations:
    /// - Line-by-line processing cannot handle multiline expressions properly
    /// - Complex nested structures (subqueries, CTEs) may not transform correctly
    /// </summary>
    /// <param name="input">SQL text with trailing commas</param>
    /// <returns>SQL text with leading commas</returns>
    public static string ToLeadingCommas(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var lineEnding = LineEndingHelpers.DetectLineEnding(input);
        var lines = LineEndingHelpers.SplitByLineEnding(input, lineEnding);
        var tracker = new ProtectedRegionTracker();
        var trailingCommaLines = new bool[lines.Length];

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Check if this line ends with a comma outside protected regions.
            // This also advances protected-region state for the current line.
            var (hasTrailingComma, commaPosition) = FindTrailingComma(line, tracker);

            if (hasTrailingComma && commaPosition >= 0)
            {
                trailingCommaLines[i] = true;
            }
        }

        for (var i = 0; i < lines.Length; i++)
        {
            if (!trailingCommaLines[i])
            {
                continue;
            }

            var targetIndex = FindLeadingCommaTargetIndex(lines, i + 1);
            if (targetIndex < 0)
            {
                continue;
            }

            lines[i] = RemoveTrailingComma(lines[i]);
            lines[targetIndex] = PrependLeadingComma(lines[targetIndex]);
        }

        var result = new StringBuilder(input.Length + 16);
        for (var i = 0; i < lines.Length; i++)
        {
            result.Append(lines[i]);

            if (i < lines.Length - 1)
            {
                result.Append(lineEnding);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Transforms leading commas to trailing commas.
    ///
    /// Example:
    /// Input:
    ///   SELECT id
    ///        , name
    ///        , email
    ///
    /// Output:
    ///   SELECT id,
    ///          name,
    ///          email
    ///
    /// Known limitations:
    /// - Line-by-line processing cannot handle multiline expressions properly
    /// - Complex nested structures (subqueries, CTEs) may not transform correctly
    /// </summary>
    /// <param name="input">SQL text with leading commas</param>
    /// <returns>SQL text with trailing commas</returns>
    public static string ToTrailingCommas(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var lineEnding = LineEndingHelpers.DetectLineEnding(input);
        var lines = LineEndingHelpers.SplitByLineEnding(input, lineEnding);
        var tracker = new ProtectedRegionTracker();
        var leadingCommaLines = new bool[lines.Length];

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Check if this line starts with a comma outside protected regions.
            // This also advances protected-region state for the current line.
            var hasLeadingComma = FindLeadingComma(line, tracker);

            if (hasLeadingComma)
            {
                leadingCommaLines[i] = true;
            }
        }

        for (var i = 0; i < lines.Length; i++)
        {
            if (!leadingCommaLines[i])
            {
                continue;
            }

            var targetIndex = FindTrailingCommaTargetIndex(lines, i - 1);
            if (targetIndex < 0)
            {
                continue;
            }

            lines[i] = RemoveLeadingComma(lines[i]);
            lines[targetIndex] = AppendTrailingComma(lines[targetIndex]);
        }

        var result = new StringBuilder(input.Length + 16);
        for (var i = 0; i < lines.Length; i++)
        {
            result.Append(lines[i]);

            if (i < lines.Length - 1)
            {
                result.Append(lineEnding);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Finds a trailing comma on a line, skipping commas inside protected regions.
    /// Returns the position of the trailing comma (or -1 if none found) and whether
    /// the line ends with a comma outside protected regions.
    /// </summary>
    private static (bool hasTrailingComma, int commaPosition) FindTrailingComma(string line, ProtectedRegionTracker tracker)
    {
        var trimmed = line.TrimEnd();
        if (string.IsNullOrEmpty(trimmed))
        {
            return (false, -1);
        }

        // Track protected regions through the line to determine if trailing comma is protected
        var lastCommaOutsideProtected = -1;

        for (var i = 0; i < trimmed.Length;)
        {
            // Check for line comment first
            if (ProtectedRegionTracker.IsLineCommentStart(trimmed, i))
            {
                // Rest of line is comment
                break;
            }

            // Try to consume or start protected region
            if (tracker.TryAdvance(trimmed, ref i))
            {
                continue;
            }

            var c = trimmed[i];

            // Track comma position if outside protected regions
            if (c == ',' && !tracker.IsInProtectedRegion())
            {
                lastCommaOutsideProtected = i;
            }

            i++;
        }

        // Check if the last non-whitespace character was a comma outside protected regions
        var isTrailing = lastCommaOutsideProtected >= 0 && lastCommaOutsideProtected == trimmed.Length - 1;

        return (isTrailing, isTrailing ? lastCommaOutsideProtected : -1);
    }

    private static string RemoveTrailingComma(string line)
    {
        var trimmed = line.TrimEnd();
        if (trimmed.Length == 0 || trimmed[^1] != ',')
        {
            return line;
        }

        return trimmed[..^1].TrimEnd();
    }

    private static int FindLeadingCommaTargetIndex(string[] lines, int startIndex)
    {
        for (var i = startIndex; i < lines.Length; i++)
        {
            if (CanReceiveLeadingComma(lines[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool CanReceiveLeadingComma(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0)
        {
            return false;
        }

        // Avoid inserting before line comments because ", -- comment" comments out the comma.
        if (trimmed.StartsWith("--", StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private static string PrependLeadingComma(string line)
    {
        var nextTrimStart = line.TrimStart();
        var leadingWhitespace = line[..^nextTrimStart.Length];

        if (nextTrimStart.Length == 0)
        {
            return leadingWhitespace + ",";
        }

        if (nextTrimStart[0] == ',')
        {
            return line;
        }

        return leadingWhitespace + ", " + nextTrimStart;
    }

    /// <summary>
    /// Finds a leading comma on a line, skipping commas inside protected regions.
    /// Returns true if the first non-whitespace content on the line is a comma
    /// outside protected regions.
    /// </summary>
    private static bool FindLeadingComma(string line, ProtectedRegionTracker tracker)
    {
        var trimmed = line.TrimStart();
        if (string.IsNullOrEmpty(trimmed))
        {
            return false;
        }

        // If already in a protected region, advance through the line to update state
        if (tracker.IsInProtectedRegion())
        {
            for (var i = 0; i < trimmed.Length;)
            {
                if (tracker.TryAdvance(trimmed, ref i))
                {
                    continue;
                }

                i++;
            }

            return false;
        }

        // Check if line starts with comma (outside protected regions)
        var hasLeadingComma = trimmed[0] == ',';

        // Advance tracker state through the entire line regardless
        for (var i = 0; i < trimmed.Length;)
        {
            if (ProtectedRegionTracker.IsLineCommentStart(trimmed, i))
            {
                break;
            }

            if (tracker.TryAdvance(trimmed, ref i))
            {
                continue;
            }

            i++;
        }

        return hasLeadingComma;
    }

    private static int FindTrailingCommaTargetIndex(string[] lines, int startIndex)
    {
        for (var i = startIndex; i >= 0; i--)
        {
            if (CanReceiveTrailingComma(lines[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool CanReceiveTrailingComma(string line)
    {
        var trimmed = line.TrimEnd();
        if (trimmed.Length == 0)
        {
            return false;
        }

        return true;
    }

    private static string RemoveLeadingComma(string line)
    {
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] != ',')
        {
            return line;
        }

        var leadingWhitespace = line[..^trimmed.Length];

        // Remove the comma and any space immediately following it
        var afterComma = trimmed[1..];
        if (afterComma.Length > 0 && afterComma[0] == ' ')
        {
            afterComma = afterComma[1..];
        }

        return leadingWhitespace + afterComma;
    }

    private static string AppendTrailingComma(string line)
    {
        var trimmed = line.TrimEnd();
        if (trimmed.Length == 0)
        {
            return line + ",";
        }

        if (trimmed[^1] == ',')
        {
            return line;
        }

        return trimmed + ",";
    }
}
