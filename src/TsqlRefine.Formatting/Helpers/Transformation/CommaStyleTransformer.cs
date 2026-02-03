using System.Text;
using TsqlRefine.Formatting.Helpers.Protection;

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
        var lines = input.Split('\n');
        var result = new StringBuilder(input.Length);
        var tracker = new ProtectedRegionTracker();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            // Check if this line ends with a comma outside protected regions
            var (hasTrailingComma, commaPosition) = FindTrailingComma(line, tracker);

            if (hasTrailingComma && commaPosition >= 0)
            {
                // This line has a trailing comma outside protected regions
                var withoutComma = line[..commaPosition].TrimEnd();
                result.Append(withoutComma);

                // If there's a next line, prepend the comma to it
                if (i + 1 < lines.Length)
                {
                    result.Append('\n');
                    var nextLine = lines[i + 1];
                    var nextTrimStart = nextLine.TrimStart();
                    var leadingWhitespace = nextLine[..^nextTrimStart.Length];
                    result.Append(leadingWhitespace);
                    result.Append(',');
                    if (nextTrimStart.Length > 0)
                    {
                        result.Append(' ');
                        result.Append(nextTrimStart);
                    }

                    // Update tracker state with the next line content
                    UpdateTrackerState(nextLine, tracker);
                    i++; // Skip next line as we already processed it
                }
                else
                {
                    // Last line with comma, keep it trailing
                    result.Append(',');
                }
            }
            else
            {
                result.Append(line);
                // Update tracker state for non-comma lines
                if (!hasTrailingComma)
                {
                    UpdateTrackerState(line, tracker);
                }
            }

            if (i < lines.Length - 1)
            {
                result.Append('\n');
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

    /// <summary>
    /// Updates the tracker state by processing a line without outputting.
    /// This keeps the tracker state in sync when we skip lines.
    /// </summary>
    private static void UpdateTrackerState(string line, ProtectedRegionTracker tracker)
    {
        for (var i = 0; i < line.Length;)
        {
            // Check for line comment first
            if (ProtectedRegionTracker.IsLineCommentStart(line, i))
            {
                // Rest of line is comment - no state change needed
                break;
            }

            // Try to consume or start protected region
            if (tracker.TryAdvance(line, ref i))
            {
                continue;
            }

            i++;
        }
    }
}
