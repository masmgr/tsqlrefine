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
    private static readonly HashSet<char> SingleCharOperators = ['=', '<', '>', '+', '-', '*', '/', '%'];

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
                ProcessSingleOperator(line, output, ref index, leadingWhitespaceEnd);
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

    private static void ProcessSingleOperator(string line, StringBuilder output, ref int index, int leadingWhitespaceEnd)
    {
        var c = line[index];

        // Check for scientific notation: digit followed by e/E followed by +/-
        if ((c == '+' || c == '-') && IsScientificNotationSign(line, index))
        {
            output.Append(c);
            index++;
            return;
        }

        // +, - can be unary operators
        // * after ( is special case: COUNT(*), could also be wildcard in SELECT *
        // =, <, >, /, % are always binary operators
        var canBeUnary = c == '+' || c == '-';

        // Special case: * after ( is not multiplication but wildcard/special syntax
        // e.g., COUNT(*), SUM(*), etc.
        if (c == '*' && IsAsteriskAfterOpenParen(output))
        {
            output.Append(c);
            index++;
            return;
        }

        // Determine if this is a binary or unary operator
        if (!canBeUnary || IsBinaryOperatorContext(output, leadingWhitespaceEnd))
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
            if (c == ' ' || c == '\t')
            {
                continue;
            }

            return c == '(';
        }

        return false;
    }

    private static bool IsScientificNotationSign(string line, int index)
    {
        // Pattern: [0-9][eE][+-][0-9]
        // Current char (at index) is + or -
        if (index < 2)
        {
            return false;
        }

        var prev = line[index - 1];
        if (prev != 'e' && prev != 'E')
        {
            return false;
        }

        // Check for digit (or dot) before 'e'/'E'
        var beforeE = line[index - 2];
        if (!char.IsDigit(beforeE) && beforeE != '.')
        {
            return false;
        }

        // Check for digit after +/-
        if (index + 1 >= line.Length)
        {
            return false;
        }

        return char.IsDigit(line[index + 1]);
    }

    private static bool IsBinaryOperatorContext(StringBuilder output, int leadingWhitespaceEnd)
    {
        // Find the last non-whitespace character in output
        var (lastNonSpace, hasSpaceBefore) = GetLastNonWhitespaceCharAndSpaceFlag(output);

        if (lastNonSpace == null)
        {
            // Nothing before operator (line start) - unary
            return false;
        }

        var lastChar = lastNonSpace.Value;

        // If there's space before the operator, we need to check if the preceding
        // token is a keyword or an operand
        if (hasSpaceBefore)
        {
            // After space, check if preceding character ends an operand
            // - Closing parenthesis ) - operand end
            // - Closing bracket ] - operand end
            // - Quote characters - operand end
            // - Digit - likely operand end (e.g., "1 -" in expressions)
            // - Letter could be keyword (SELECT, WHERE, AND, etc.) or identifier
            //   We treat space + letter as unary context (conservative approach)
            //   since keywords like SELECT, WHERE, WHEN, etc. are followed by operands
            if (lastChar == ')' || lastChar == ']' || lastChar == '\'' || lastChar == '"')
            {
                return true;
            }

            // Digit followed by space then operator is binary: "1 + 2"
            if (char.IsDigit(lastChar))
            {
                return true;
            }

            // Letter/underscore followed by space: could be keyword or identifier
            // Conservative: treat as unary context (SELECT -1, WHERE -x, etc.)
            // This means "a -1" becomes "a -1" not "a - 1", but this is safer
            // Users who want binary spacing should write without preceding space: "a-1" -> "a - 1"
            return false;
        }

        // No space before operator - check for binary context
        // Binary operator context: immediately after identifier/number/closing delimiter
        // - Alphanumeric or underscore (identifier/number end): a+b, 1+2
        // - Closing parenthesis ): (a)+b
        // - Closing bracket ]: [a]+b
        // - Quote characters: 'a'+b
        if (char.IsLetterOrDigit(lastChar) || lastChar == '_' ||
            lastChar == ')' || lastChar == ']' ||
            lastChar == '\'' || lastChar == '"')
        {
            return true;
        }

        // Unary context: after opening delimiter or operator
        // - Opening parenthesis (
        // - Comma ,
        // - Operators: =, <, >, +, -, *, /, %
        // These indicate the operator is unary
        return false;
    }

    private static (char? lastNonSpace, bool hasSpaceBefore) GetLastNonWhitespaceCharAndSpaceFlag(StringBuilder sb)
    {
        var hasSpace = false;
        for (var i = sb.Length - 1; i >= 0; i--)
        {
            var c = sb[i];
            if (c == ' ' || c == '\t')
            {
                hasSpace = true;
                continue;
            }

            return (c, hasSpace);
        }

        return (null, hasSpace);
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
