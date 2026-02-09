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
            if (trailingCommaLines[i] && i + 1 < lines.Length)
            {
                lines[i] = RemoveTrailingComma(lines[i]);
                lines[i + 1] = PrependLeadingComma(lines[i + 1]);
            }
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
    /// TODO: Implement for future use.
    /// This would provide symmetry with ToLeadingCommas and allow round-trip transformations.
    /// </summary>
    /// <param name="input">SQL text with leading commas</param>
    /// <returns>SQL text with trailing commas</returns>
    /// <exception cref="NotImplementedException">This transformation is not yet implemented</exception>
    public static string ToTrailingCommas(string input)
    {
        // Not currently used, but provides symmetry
        throw new NotImplementedException("Trailing comma transformation not yet implemented");
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
}
