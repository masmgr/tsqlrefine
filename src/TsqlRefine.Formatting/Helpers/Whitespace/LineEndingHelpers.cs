using System.Text;

namespace TsqlRefine.Formatting.Helpers.Whitespace;

/// <summary>
/// Shared utilities for line ending detection and manipulation.
/// </summary>
internal static class LineEndingHelpers
{
    /// <summary>
    /// Detects the line ending used in the input string.
    /// CRLF takes precedence over LF when both are present.
    /// </summary>
    /// <param name="input">The input string to analyze</param>
    /// <param name="defaultEnding">Default line ending if none detected</param>
    /// <returns>Detected line ending: "\r\n", "\n", or the default</returns>
    public static string DetectLineEnding(string input, string defaultEnding = "\n")
    {
        // Check for CRLF first (before checking for LF alone)
        if (input.Contains("\r\n"))
        {
            return "\r\n";
        }

        if (input.Contains('\n'))
        {
            return "\n";
        }

        return defaultEnding;
    }

    /// <summary>
    /// Splits input string by the specified line ending.
    /// </summary>
    /// <param name="input">The input string to split</param>
    /// <param name="lineEnding">The line ending to split by</param>
    /// <returns>Array of lines</returns>
    public static string[] SplitByLineEnding(string input, string lineEnding)
    {
        // For CRLF, split by \r\n directly
        if (lineEnding == "\r\n")
        {
            return input.Split(["\r\n"], StringSplitOptions.None);
        }

        // For LF, split by \n
        return input.Split('\n');
    }

    /// <summary>
    /// Removes standalone CR characters (\r not followed by \n) from the input.
    /// CRLF sequences (\r\n) are preserved intact.
    /// </summary>
    /// <param name="input">The input string to process</param>
    /// <returns>String with standalone CR characters removed</returns>
    public static string StripStandaloneCr(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        // Fast path: if no CR at all, return unchanged
        if (!input.Contains('\r'))
        {
            return input;
        }

        var sb = new StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c == '\r')
            {
                // Keep CR only if followed by LF (CRLF pair)
                if (i + 1 < input.Length && input[i + 1] == '\n')
                {
                    sb.Append('\r');
                }

                // else: standalone CR, skip it
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Applies a line-by-line transformation while preserving original line ending style.
    /// </summary>
    /// <param name="input">The input text to transform.</param>
    /// <param name="transformLine">Line transformation callback. Second argument is zero-based line index.</param>
    /// <returns>Transformed text.</returns>
    public static string TransformLines(string input, Func<string, int, string> transformLine)
    {
        ArgumentNullException.ThrowIfNull(transformLine);

        var lineEnding = DetectLineEnding(input);
        var lines = SplitByLineEnding(input, lineEnding);

        var result = new StringBuilder(input.Length + 16);
        for (var i = 0; i < lines.Length; i++)
        {
            result.Append(transformLine(lines[i], i));

            if (i < lines.Length - 1)
            {
                result.Append(lineEnding);
            }
        }

        return result.ToString();
    }
}
