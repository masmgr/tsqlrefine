using System.Text;
using TsqlRefine.Formatting.Helpers.Protection;

namespace TsqlRefine.Formatting.Helpers.Whitespace;

/// <summary>
/// Normalizes inline spacing within SQL lines while preserving protected regions.
///
/// Transformations:
/// - Adds space after commas (e.g., "a,b" → "a, b")
/// - Preserves spaces inside strings, comments, brackets
/// - Does not modify leading indentation (handled by WhitespaceNormalizer)
///
/// Known limitations:
/// - Does not preserve visual alignment (e.g., columnar spacing)
/// - Simple character-by-character processing
/// </summary>
public static class InlineSpaceNormalizer
{
    /// <summary>
    /// Normalizes inline spacing in SQL text.
    /// </summary>
    /// <param name="input">SQL text to normalize</param>
    /// <param name="options">Formatting options</param>
    /// <returns>SQL text with normalized inline spacing</returns>
    public static string Normalize(string input, FormattingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        if (!options.NormalizeInlineSpacing)
        {
            return input;
        }

        // Create tracker outside the loop to preserve state across lines
        // (e.g., multi-line block comments)
        var tracker = new ProtectedRegionTracker();
        return LineEndingHelpers.TransformLines(input, (line, _) => NormalizeLine(line, tracker));
    }

    private static string NormalizeLine(string line, ProtectedRegionTracker tracker)
    {
        if (string.IsNullOrEmpty(line))
        {
            return line;
        }

        var output = new StringBuilder();
        var needsSpaceAfterComma = false;
        var index = 0;

        // If we're inside a protected region (e.g., multi-line block comment),
        // consume characters until we exit the region
        while (index < line.Length && tracker.IsInProtectedRegion())
        {
            if (tracker.TryConsume(line, output, ref index))
            {
                continue;
            }

            // Should not happen if TryConsume works correctly, but safeguard
            output.Append(line[index]);
            index++;
        }

        // Preserve leading whitespace (only relevant if we weren't in a protected region)
        var leadingWhitespaceEnd = index;
        while (leadingWhitespaceEnd < line.Length && (line[leadingWhitespaceEnd] == ' ' || line[leadingWhitespaceEnd] == '\t'))
        {
            leadingWhitespaceEnd++;
        }

        // Copy leading whitespace as-is
        if (leadingWhitespaceEnd > index)
        {
            output.Append(line.AsSpan(index, leadingWhitespaceEnd - index));
            index = leadingWhitespaceEnd;
        }

        while (index < line.Length)
        {
            // Try to consume characters in active protected region
            if (tracker.TryConsume(line, output, ref index))
            {
                needsSpaceAfterComma = false;
                continue;
            }

            // Try to start a new protected region
            if (tracker.TryStartProtectedRegion(line, output, ref index))
            {
                needsSpaceAfterComma = false;
                continue;
            }

            // Handle line comments specially - preserve rest of line as-is
            var inLineComment = false;
            if (ProtectedRegionTracker.TryStartLineComment(line, output, ref index, ref inLineComment))
            {
                break; // Line comment consumes rest of line
            }

            var c = line[index];

            // Handle comma
            if (c == ',')
            {
                // Remove trailing inline whitespace before comma.
                // Keep indentation by not trimming before leadingWhitespaceEnd.
                while (output.Length > leadingWhitespaceEnd &&
                       (output[^1] == ' ' || output[^1] == '\t'))
                {
                    output.Length--;
                }

                output.Append(c);
                needsSpaceAfterComma = true;
                index++;
                continue;
            }

            // Handle space or tab
            if (c == ' ' || c == '\t')
            {
                if (needsSpaceAfterComma)
                {
                    // Preserve the original whitespace character (space or tab) after comma
                    output.Append(c);
                    needsSpaceAfterComma = false;
                    index++;
                    continue;
                }

                // Output the space/tab
                output.Append(c);
                index++;
                continue;
            }

            // Regular character
            if (needsSpaceAfterComma)
            {
                // Add space before this character
                output.Append(' ');
                needsSpaceAfterComma = false;
            }

            output.Append(c);
            index++;
        }

        return output.ToString();
    }

}
