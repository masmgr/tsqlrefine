using System.Text;
using TsqlRefine.Formatting.Helpers.Protection;

namespace TsqlRefine.Formatting.Helpers.Whitespace;

/// <summary>
/// Normalizes consecutive blank lines and optionally trims leading blank lines,
/// while preserving blank lines inside protected regions (block comments, string literals).
/// </summary>
public static class BlankLineNormalizer
{
    /// <summary>
    /// Normalizes blank lines in SQL text according to formatting options.
    /// </summary>
    /// <param name="input">SQL text to normalize</param>
    /// <param name="options">Formatting options controlling blank line normalization</param>
    /// <returns>SQL text with normalized blank lines</returns>
    public static string Normalize(string input, FormattingOptions options)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var maxBlank = options.MaxConsecutiveBlankLines;
        var trimLeading = options.TrimLeadingBlankLines;

        // Fast path: nothing to do
        if (maxBlank == 0 && !trimLeading)
        {
            return input;
        }

        var lineEnding = LineEndingHelpers.DetectLineEnding(input);
        var lines = LineEndingHelpers.SplitByLineEnding(input, lineEnding);

        // Detect whether input ends with a line ending (final element is empty after split)
        var endsWithNewline = lines.Length > 1 && lines[^1].Length == 0;

        var tracker = new ProtectedRegionTracker();
        var result = new StringBuilder(input.Length + 16);
        var consecutiveBlankCount = 0;
        var pastLeadingBlanks = false;
        var firstLineWritten = false;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];

            // Handle trailing empty element from split (preserves final newline)
            if (lineIndex == lines.Length - 1 && endsWithNewline && line.Length == 0)
            {
                // Append the final line ending to preserve it
                result.Append(lineEnding);
                break;
            }

            var isProtected = tracker.IsInProtectedRegion();
            var isBlank = !isProtected && IsBlankLine(line);

            // Advance tracker state through this line's characters
            AdvanceTrackerThroughLine(tracker, line);

            if (isBlank && !pastLeadingBlanks)
            {
                // Still in leading blank lines region
                if (trimLeading)
                {
                    continue;
                }
            }
            else if (!isBlank)
            {
                pastLeadingBlanks = true;
            }

            if (isBlank && pastLeadingBlanks)
            {
                consecutiveBlankCount++;

                // If max is configured and we've exceeded it, skip this blank line
                if (maxBlank > 0 && consecutiveBlankCount > maxBlank)
                {
                    continue;
                }
            }
            else if (!isBlank)
            {
                consecutiveBlankCount = 0;
            }

            // Append line ending before each line except the first
            if (firstLineWritten)
            {
                result.Append(lineEnding);
            }

            result.Append(line);
            firstLineWritten = true;
        }

        return result.ToString();
    }

    /// <summary>
    /// Determines whether a line is blank (empty or whitespace-only).
    /// </summary>
    private static bool IsBlankLine(string line)
    {
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] is not (' ' or '\t'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Advances the protected region tracker through all characters in a line
    /// so that it correctly tracks block comment and string literal state across lines.
    /// </summary>
    private static void AdvanceTrackerThroughLine(ProtectedRegionTracker tracker, string line)
    {
        var i = 0;
        while (i < line.Length)
        {
            // If inside a protected region or starting one, TryAdvance handles it
            if (tracker.TryAdvance(line, ref i))
            {
                continue;
            }

            // Check for line comment start (-- to end of line): skip rest of line
            if (ProtectedRegionTracker.IsLineCommentStart(line, i))
            {
                break;
            }

            i++;
        }
    }
}
