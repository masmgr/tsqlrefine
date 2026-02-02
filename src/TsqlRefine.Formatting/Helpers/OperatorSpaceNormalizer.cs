using System.Collections.Frozen;
using System.Text;

namespace TsqlRefine.Formatting.Helpers;

/// <summary>
/// Normalizes spacing around binary operators while preserving unary operators
/// and scientific notation.
///
/// Transformations:
/// - Adds space before and after binary operators: a=b → a = b
/// - Preserves unary operators: SELECT -1 (not SELECT - 1)
/// - Preserves scientific notation: 1e-3 (not 1e - 3)
/// - Preserves operators inside strings, comments, brackets
///
/// Supported operators: =, &lt;&gt;, !=, &lt;, &gt;, &lt;=, &gt;=, +, -, *, /, %
///
/// Known limitations:
/// - Cannot distinguish all edge cases without full parsing
/// - Bitwise operators (~, &amp;, |, ^) not normalized
/// </summary>
public static class OperatorSpaceNormalizer
{
    private static readonly FrozenSet<char> SingleCharOperators =
        FrozenSet.ToFrozenSet(['=', '<', '>', '+', '-', '*', '/', '%']);

    /// <summary>
    /// Characters that can end an operand (indicate preceding token is a value).
    /// </summary>
    private static readonly FrozenSet<char> OperandEndingChars =
        FrozenSet.ToFrozenSet([')', ']', '\'', '"']);

    /// <summary>
    /// Normalizes operator spacing in SQL text.
    /// </summary>
    /// <param name="input">SQL text to normalize</param>
    /// <param name="options">Formatting options</param>
    /// <returns>SQL text with normalized operator spacing</returns>
    public static string Normalize(string input, FormattingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        if (!options.NormalizeOperatorSpacing)
        {
            return input;
        }

        var lineEnding = LineEndingHelpers.DetectLineEnding(input);
        var lines = LineEndingHelpers.SplitByLineEnding(input, lineEnding);
        var result = new StringBuilder();

        for (var i = 0; i < lines.Length; i++)
        {
            var processedLine = NormalizeLine(lines[i]);
            result.Append(processedLine);

            if (i < lines.Length - 1)
            {
                result.Append(lineEnding);
            }
        }

        return result.ToString();
    }

    private static string NormalizeLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return line;
        }

        var tracker = new ProtectedRegionTracker();
        var output = new StringBuilder();
        var index = 0;

        // Preserve leading whitespace
        var leadingWhitespaceEnd = 0;
        while (leadingWhitespaceEnd < line.Length && (line[leadingWhitespaceEnd] == ' ' || line[leadingWhitespaceEnd] == '\t'))
        {
            leadingWhitespaceEnd++;
        }

        if (leadingWhitespaceEnd > 0)
        {
            output.Append(line.AsSpan(0, leadingWhitespaceEnd));
            index = leadingWhitespaceEnd;
        }

        while (index < line.Length)
        {
            // Try to consume characters in active protected region
            if (tracker.TryConsume(line, output, ref index))
            {
                continue;
            }

            // Try to start a new protected region
            if (tracker.TryStartProtectedRegion(line, output, ref index))
            {
                continue;
            }

            // Handle line comments (-- style) - must check before processing minus operator
            var inLineComment = false;
            if (ProtectedRegionTracker.TryStartLineComment(line, output, ref index, ref inLineComment))
            {
                break;
            }

            var c = line[index];

            // Try compound operators first (<>, !=, <=, >=)
            if (TryProcessCompoundOperator(line, output, ref index))
            {
                continue;
            }

            // Try single-char operators (=, <, >, +, -, *, /, %)
            if (SingleCharOperators.Contains(c))
            {
                ProcessSingleOperator(line, output, ref index);
                continue;
            }

            // Regular character
            output.Append(c);
            index++;
        }

        return output.ToString();
    }

    private static bool TryProcessCompoundOperator(string line, StringBuilder output, ref int index)
    {
        if (index + 1 >= line.Length)
        {
            return false;
        }

        var c1 = line[index];
        var c2 = line[index + 1];

        // Check for compound operators: <>, !=, <=, >=
        var isCompound = (c1 == '<' && c2 == '>') ||
                         (c1 == '!' && c2 == '=') ||
                         (c1 == '<' && c2 == '=') ||
                         (c1 == '>' && c2 == '=');

        if (!isCompound)
        {
            return false;
        }

        // These are always binary operators - ensure spacing
        EnsureSpaceBefore(output);
        output.Append(c1);
        output.Append(c2);
        index += 2;
        EnsureSpaceAfter(line, output, ref index);

        return true;
    }

    private static void ProcessSingleOperator(string line, StringBuilder output, ref int index)
    {
        var c = line[index];

        // Check for scientific notation: digit followed by e/E followed by +/-
        if (c is '+' or '-' && ScientificNotationChecker.IsSign(line, index))
        {
            output.Append(c);
            index++;
            return;
        }

        // +, - can be unary operators
        // * after ( is special case: COUNT(*), could also be wildcard in SELECT *
        // =, <, >, /, % are always binary operators
        var canBeUnary = c is '+' or '-';

        // Special case: * after ( is not multiplication but wildcard/special syntax
        // e.g., COUNT(*), SUM(*), etc.
        if (c == '*' && IsAsteriskAfterOpenParen(output))
        {
            output.Append(c);
            index++;
            return;
        }

        // Special case: */ is block comment closing - don't treat as operators
        // This handles multi-line block comments where the tracker state is not preserved
        if (c == '*' && index + 1 < line.Length && line[index + 1] == '/')
        {
            output.Append("*/");
            index += 2;
            return;
        }

        // Determine if this is a binary or unary operator
        if (!canBeUnary || BinaryContextChecker.IsBinaryContext(output))
        {
            EnsureSpaceBefore(output);
            output.Append(c);
            index++;
            EnsureSpaceAfter(line, output, ref index);
        }
        else
        {
            // Unary operator - no space before
            output.Append(c);
            index++;
        }
    }

    private static bool IsAsteriskAfterOpenParen(StringBuilder output)
    {
        // Check if the last non-whitespace character is (
        // This handles COUNT(*), SUM(*), etc.
        for (var i = output.Length - 1; i >= 0; i--)
        {
            var c = output[i];
            if (c is ' ' or '\t')
            {
                continue;
            }

            return c == '(';
        }

        return false;
    }

    /// <summary>
    /// Detects scientific notation patterns like 1e-3, 2.5E+10.
    /// </summary>
    private static class ScientificNotationChecker
    {
        /// <summary>
        /// Checks if +/- at the given index is part of scientific notation.
        /// Pattern: [digit or .][eE][+-][digit]
        /// </summary>
        public static bool IsSign(string line, int index)
        {
            // Need at least 2 chars before and 1 after
            if (index < 2 || index + 1 >= line.Length)
            {
                return false;
            }

            var prev = line[index - 1];
            if (prev is not ('e' or 'E'))
            {
                return false;
            }

            var beforeE = line[index - 2];
            if (!char.IsDigit(beforeE) && beforeE != '.')
            {
                return false;
            }

            return char.IsDigit(line[index + 1]);
        }
    }

    /// <summary>
    /// Determines whether an operator is in binary (e.g., a + b) or unary (e.g., -1) context.
    /// </summary>
    private static class BinaryContextChecker
    {
        /// <summary>
        /// Determines if the current position indicates a binary operator context.
        /// </summary>
        /// <param name="output">The output buffer built so far.</param>
        /// <returns>True if operator should be treated as binary; false for unary.</returns>
        public static bool IsBinaryContext(StringBuilder output)
        {
            var (lastNonSpace, hasSpaceBefore) = GetLastNonWhitespaceInfo(output);

            if (lastNonSpace is null)
            {
                // Nothing before operator (line start) - unary
                return false;
            }

            var lastChar = lastNonSpace.Value;

            return hasSpaceBefore
                ? IsOperandEndAfterSpace(lastChar)
                : IsOperandEndNoSpace(lastChar);
        }

        /// <summary>
        /// After space, check if preceding character ends an operand.
        /// Closing delimiters, quotes, and digits indicate binary context.
        /// Letters/underscores are treated as unary (conservative: SELECT -1).
        /// </summary>
        private static bool IsOperandEndAfterSpace(char c)
        {
            if (OperandEndingChars.Contains(c))
            {
                return true;
            }

            // Digit followed by space then operator is binary: "1 + 2"
            return char.IsDigit(c);
        }

        /// <summary>
        /// No space before operator - check for binary context.
        /// Alphanumeric, underscore, or closing delimiters indicate binary.
        /// </summary>
        private static bool IsOperandEndNoSpace(char c)
        {
            return char.IsLetterOrDigit(c) ||
                   c == '_' ||
                   OperandEndingChars.Contains(c);
        }

        private static (char? lastChar, bool hasSpaceBefore) GetLastNonWhitespaceInfo(StringBuilder sb)
        {
            var hasSpace = false;
            for (var i = sb.Length - 1; i >= 0; i--)
            {
                var c = sb[i];
                if (c is ' ' or '\t')
                {
                    hasSpace = true;
                    continue;
                }

                return (c, hasSpace);
            }

            return (null, hasSpace);
        }
    }

    private static void EnsureSpaceBefore(StringBuilder output)
    {
        if (output.Length == 0)
        {
            return;
        }

        // Remove trailing whitespace first
        while (output.Length > 0 && (output[^1] == ' ' || output[^1] == '\t'))
        {
            output.Length--;
        }

        // Add single space if there's content
        if (output.Length > 0)
        {
            output.Append(' ');
        }
    }

    private static void EnsureSpaceAfter(string line, StringBuilder output, ref int index)
    {
        // Skip existing whitespace in input
        while (index < line.Length && (line[index] == ' ' || line[index] == '\t'))
        {
            index++;
        }

        // Add single space if there's more content (and not at end of line)
        if (index < line.Length)
        {
            output.Append(' ');
        }
    }

}
