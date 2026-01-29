using System.Text;

namespace TsqlRefine.Formatting.Helpers;

/// <summary>
/// Transforms SQL between trailing and leading comma styles.
/// </summary>
public static class CommaStyleTransformer
{
    /// <summary>
    /// Transforms trailing commas to leading commas.
    ///
    /// Current implementation is naive and handles simple cases only.
    /// TODO: Implement AST-based approach for complex scenarios (subqueries, CTEs, nested structures).
    ///
    /// Known limitations:
    /// - Does not detect commas inside strings or comments
    /// - May incorrectly transform commas in complex nested structures
    /// - Line-by-line processing cannot handle multiline expressions properly
    /// </summary>
    /// <param name="input">SQL text with trailing commas</param>
    /// <returns>SQL text with leading commas</returns>
    public static string ToLeadingCommas(string input)
    {
        // Simple transformation: move trailing commas to leading position
        // This is a basic implementation that handles simple cases
        // For more complex scenarios, a full AST-based approach would be needed
        var lines = input.Split('\n');
        var result = new StringBuilder(input.Length);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimEnd();

            if (trimmed.EndsWith(','))
            {
                // This line has a trailing comma
                var withoutComma = trimmed[..^1].TrimEnd();
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
}
