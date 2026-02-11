using System.Collections.Frozen;
using System.Text;
using TsqlRefine.Formatting.Helpers.Protection;
using TsqlRefine.Formatting.Helpers.Transformation;

namespace TsqlRefine.Formatting.Helpers.Whitespace;

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

    private static readonly FrozenSet<string> UnaryPrefixKeywords =
        FrozenSet.ToFrozenSet(
        [
            "SELECT", "WHERE", "AND", "OR", "NOT", "WHEN", "THEN", "ELSE",
            "BY", "ON", "FROM", "JOIN", "INTO", "VALUES", "RETURN", "CASE", "AS", "TOP"
        ],
        StringComparer.OrdinalIgnoreCase);

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
        => Normalize(input, options, positionMap: null);

    /// <summary>
    /// Normalizes operator spacing in SQL text with optional AST-based context.
    /// </summary>
    /// <param name="input">SQL text to normalize</param>
    /// <param name="options">Formatting options</param>
    /// <param name="positionMap">Optional AST position map for accurate operator context detection</param>
    /// <returns>SQL text with normalized operator spacing</returns>
    public static string Normalize(string input, FormattingOptions options, AstPositionMap? positionMap)
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

        // Create tracker outside the loop to preserve state across lines
        // (e.g., multi-line block comments)
        var tracker = new ProtectedRegionTracker();
        return LineEndingHelpers.TransformLines(
            input,
            (line, lineIndex) => NormalizeLine(line, tracker, positionMap, lineNumber: lineIndex + 1));
    }

    private static string NormalizeLine(string line, ProtectedRegionTracker tracker, AstPositionMap? positionMap, int lineNumber)
    {
        if (string.IsNullOrEmpty(line))
        {
            return line;
        }

        var output = new StringBuilder();
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
            // Column is 1-based in ScriptDom
            if (TryProcessCompoundOperator(line, output, ref index, positionMap, lineNumber))
            {
                continue;
            }

            // Try single-char operators (=, <, >, +, -, *, /, %)
            if (SingleCharOperators.Contains(c))
            {
                ProcessSingleOperator(line, output, ref index, positionMap, lineNumber);
                continue;
            }

            // Regular character
            output.Append(c);
            index++;
        }

        return output.ToString();
    }

    private static bool TryProcessCompoundOperator(string line, StringBuilder output, ref int index, AstPositionMap? positionMap, int lineNumber)
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

    private static void ProcessSingleOperator(string line, StringBuilder output, ref int index, AstPositionMap? positionMap, int lineNumber)
    {
        var c = line[index];
        // Column is 1-based in ScriptDom
        var column = index + 1;

        // Check for scientific notation: digit followed by e/E followed by +/-
        if (c is '+' or '-' && ScientificNotationChecker.IsSign(line, index))
        {
            output.Append(c);
            index++;
            return;
        }

        // Check AST-based context if available
        if (positionMap is not null)
        {
            var astContext = positionMap.GetContext(lineNumber, column);
            if (astContext != AstPositionMap.OperatorContext.Unknown)
            {
                ProcessOperatorWithAstContext(line, output, ref index, c, astContext);
                return;
            }
        }

        // Fall back to heuristic-based detection
        ProcessOperatorWithHeuristics(line, output, ref index, c);
    }

    /// <summary>
    /// Process operator using AST-determined context.
    /// </summary>
    private static void ProcessOperatorWithAstContext(string line, StringBuilder output, ref int index, char c, AstPositionMap.OperatorContext context)
    {
        switch (context)
        {
            case AstPositionMap.OperatorContext.UnarySign:
                // Unary operator - no space before
                output.Append(c);
                index++;
                break;

            case AstPositionMap.OperatorContext.SelectStar:
            case AstPositionMap.OperatorContext.QualifiedStar:
            case AstPositionMap.OperatorContext.FunctionStar:
                // Asterisk in non-multiplication context - no spacing
                output.Append(c);
                index++;
                break;

            case AstPositionMap.OperatorContext.BinaryArithmetic:
            case AstPositionMap.OperatorContext.Comparison:
            default:
                // Binary operator - ensure spacing
                EnsureSpaceBefore(output);
                output.Append(c);
                index++;
                EnsureSpaceAfter(line, output, ref index);
                break;
        }
    }

    /// <summary>
    /// Process operator using heuristic-based context detection (original logic).
    /// </summary>
    private static void ProcessOperatorWithHeuristics(string line, StringBuilder output, ref int index, char c)
    {
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

        // Special case: qualified star (t.*) should not be spaced as multiplication
        if (c == '*' && IsAsteriskAfterDot(output))
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

    private static bool IsAsteriskAfterDot(StringBuilder output)
    {
        // Check if the last non-whitespace character is .
        for (var i = output.Length - 1; i >= 0; i--)
        {
            var c = output[i];
            if (c is ' ' or '\t')
            {
                continue;
            }

            return c == '.';
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
            var (lastNonSpace, hasSpaceBefore, lastIndex) = GetLastNonWhitespaceInfo(output);

            if (lastNonSpace is null)
            {
                // Nothing before operator (line start) - unary
                return false;
            }

            var lastChar = lastNonSpace.Value;

            return hasSpaceBefore
                ? IsOperandEndAfterSpace(output, lastIndex, lastChar)
                : IsOperandEndNoSpace(lastChar);
        }

        /// <summary>
        /// After space, check if preceding character ends an operand.
        /// Closing delimiters, quotes, and digits indicate binary context.
        /// Letters/underscores are treated as unary (conservative: SELECT -1).
        /// </summary>
        private static bool IsOperandEndAfterSpace(StringBuilder output, int lastIndex, char c)
        {
            if (OperandEndingChars.Contains(c))
            {
                return true;
            }

            // Digit followed by space then operator is binary: "1 + 2"
            if (char.IsDigit(c) || c == '_')
            {
                return true;
            }

            if (!char.IsLetter(c))
            {
                return false;
            }

            // Distinguish "a - b" (binary) from "SELECT -1" (unary after keyword).
            var previousWord = GetWordEndingAt(output, lastIndex);
            return !UnaryPrefixKeywords.Contains(previousWord);
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

        private static string GetWordEndingAt(StringBuilder sb, int endIndex)
        {
            if (endIndex < 0 || endIndex >= sb.Length)
            {
                return string.Empty;
            }

            var start = endIndex;
            while (start >= 0)
            {
                var c = sb[start];
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    break;
                }

                start--;
            }

            var wordStart = start + 1;
            if (wordStart > endIndex)
            {
                return string.Empty;
            }

            var length = endIndex - wordStart + 1;
            return sb.ToString(wordStart, length);
        }

        private static (char? lastChar, bool hasSpaceBefore, int lastIndex) GetLastNonWhitespaceInfo(StringBuilder sb)
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

                return (c, hasSpace, i);
            }

            return (null, hasSpace, -1);
        }
    }

    private static void EnsureSpaceBefore(StringBuilder output)
    {
        if (output.Length == 0)
        {
            return;
        }

        // If already has whitespace, preserve it (maintains alignment)
        if (output[^1] == ' ' || output[^1] == '\t')
        {
            return;
        }

        // Add single space if no whitespace exists
        output.Append(' ');
    }

    private static void EnsureSpaceAfter(string line, StringBuilder output, ref int index)
    {
        // If next char is whitespace, preserve existing spacing (maintains alignment)
        if (index < line.Length && (line[index] == ' ' || line[index] == '\t'))
        {
            return;
        }

        // Add single space if no whitespace and there's more content
        if (index < line.Length)
        {
            output.Append(' ');
        }
    }

}
