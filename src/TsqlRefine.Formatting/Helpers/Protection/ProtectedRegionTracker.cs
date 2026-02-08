using System.Text;

namespace TsqlRefine.Formatting.Helpers.Protection;

/// <summary>
/// Tracks protected regions (strings, comments, brackets) during whitespace normalization.
/// Encapsulates state machine logic for region detection and consumption.
/// </summary>
internal sealed class ProtectedRegionTracker
{
    private bool _inString;
    private bool _inDoubleQuote;
    private bool _inBracket;
    private int _blockCommentDepth;

    /// <summary>
    /// Checks if currently inside a protected region.
    /// </summary>
    /// <returns>True if inside a string, comment, or bracket; false otherwise</returns>
    public bool IsInProtectedRegion() =>
        _inString || _blockCommentDepth > 0 || _inDoubleQuote || _inBracket;

    /// <summary>
    /// Updates tracker state for a character without producing output.
    /// Returns true if the character was consumed (in a protected region or started one).
    /// </summary>
    /// <param name="text">The text being processed</param>
    /// <param name="index">Current index (will be advanced if consumed)</param>
    /// <returns>True if character was consumed; false otherwise</returns>
    public bool TryAdvance(string text, ref int index)
    {
        return TryConsumeWithoutOutput(text, ref index) ||
               TryStartProtectedRegionWithoutOutput(text, ref index);
    }

    /// <summary>
    /// Checks if text at the given index starts a line comment (-- style).
    /// Does not consume or modify state - caller should skip to end of line.
    /// </summary>
    public static bool IsLineCommentStart(string text, int index)
    {
        return index + 1 < text.Length && text[index] == '-' && text[index + 1] == '-';
    }

    /// <summary>
    /// Attempts to consume characters in an active protected region.
    /// </summary>
    /// <param name="text">The text being processed</param>
    /// <param name="output">Output buffer to append to</param>
    /// <param name="index">Current index (will be advanced if consumed)</param>
    /// <returns>True if characters were consumed; false if not in a protected region</returns>
    public bool TryConsume(string text, StringBuilder output, ref int index)
    {
        return TryConsumeString(text, output, ref index) ||
               TryConsumeDoubleQuote(text, output, ref index) ||
               TryConsumeBracket(text, output, ref index) ||
               TryConsumeBlockComment(text, output, ref index);
    }

    /// <summary>
    /// Attempts to start a new protected region.
    /// </summary>
    /// <param name="text">The text being processed</param>
    /// <param name="output">Output buffer to append to</param>
    /// <param name="index">Current index (will be advanced if region started)</param>
    /// <returns>True if a protected region was started; false otherwise</returns>
    public bool TryStartProtectedRegion(string text, StringBuilder output, ref int index)
    {
        return TryStartBlockComment(text, output, ref index) ||
               TryStartString(text, output, ref index) ||
               TryStartDoubleQuote(text, output, ref index) ||
               TryStartBracket(text, output, ref index);
    }

    /// <summary>
    /// Attempts to detect and consume a line comment (-- style).
    /// Line comments are handled specially as they consume the rest of the line.
    /// </summary>
    /// <param name="text">The text being processed</param>
    /// <param name="output">Output buffer to append to</param>
    /// <param name="index">Current index (will be advanced if consumed)</param>
    /// <param name="inLineComment">Flag indicating if in a line comment</param>
    /// <returns>True if a line comment was started; false otherwise</returns>
    public static bool TryStartLineComment(string text, StringBuilder output, ref int index, ref bool inLineComment)
    {
        var c = text[index];
        if (c == '-' && index + 1 < text.Length && text[index + 1] == '-')
        {
            inLineComment = true;
            output.Append(text.AsSpan(index));
            index = text.Length;
            return true;
        }

        return false;
    }

    private bool TryConsumeString(string text, StringBuilder output, ref int index)
    {
        if (!_inString)
        {
            return false;
        }

        var c = text[index];
        if (c == '\'')
        {
            // Handle escaped single quotes
            if (index + 1 < text.Length && text[index + 1] == '\'')
            {
                output.Append("''");
                index += 2;
                return true;
            }

            output.Append(c);
            _inString = false;
            index++;
            return true;
        }

        output.Append(c);
        index++;
        return true;
    }

    private bool TryConsumeDoubleQuote(string text, StringBuilder output, ref int index)
    {
        if (!_inDoubleQuote)
        {
            return false;
        }

        var c = text[index];
        output.Append(c);
        index++;
        if (c == '"')
        {
            _inDoubleQuote = false;
        }

        return true;
    }

    private bool TryConsumeBracket(string text, StringBuilder output, ref int index)
    {
        if (!_inBracket)
        {
            return false;
        }

        var c = text[index];
        if (c == ']')
        {
            // Handle escaped brackets
            if (index + 1 < text.Length && text[index + 1] == ']')
            {
                output.Append("]]");
                index += 2;
                return true;
            }

            output.Append(c);
            _inBracket = false;
            index++;
            return true;
        }

        output.Append(c);
        index++;
        return true;
    }

    private bool TryConsumeBlockComment(string text, StringBuilder output, ref int index)
    {
        if (_blockCommentDepth <= 0)
        {
            return false;
        }

        var c = text[index];
        if (c == '/' && index + 1 < text.Length && text[index + 1] == '*')
        {
            _blockCommentDepth++;
            output.Append("/*");
            index += 2;
            return true;
        }

        if (c == '*' && index + 1 < text.Length && text[index + 1] == '/')
        {
            output.Append("*/");
            _blockCommentDepth--;
            index += 2;
            return true;
        }

        output.Append(c);
        index++;
        return true;
    }

    private bool TryStartBlockComment(string text, StringBuilder output, ref int index)
    {
        var c = text[index];
        if (c == '/' && index + 1 < text.Length && text[index + 1] == '*')
        {
            _blockCommentDepth = 1;
            output.Append("/*");
            index += 2;
            return true;
        }

        return false;
    }

    private bool TryStartString(string text, StringBuilder output, ref int index)
    {
        var c = text[index];
        if (c != '\'')
        {
            return false;
        }

        _inString = true;
        output.Append(c);
        index++;
        return true;
    }

    private bool TryStartDoubleQuote(string text, StringBuilder output, ref int index)
    {
        var c = text[index];
        if (c != '"')
        {
            return false;
        }

        _inDoubleQuote = true;
        output.Append(c);
        index++;
        return true;
    }

    private bool TryStartBracket(string text, StringBuilder output, ref int index)
    {
        var c = text[index];
        if (c != '[')
        {
            return false;
        }

        _inBracket = true;
        output.Append(c);
        index++;
        return true;
    }

    private bool TryConsumeWithoutOutput(string text, ref int index)
    {
        if (_inString)
        {
            return TryConsumeStringWithoutOutput(text, ref index);
        }

        if (_inDoubleQuote)
        {
            return TryConsumeDoubleQuoteWithoutOutput(text, ref index);
        }

        if (_inBracket)
        {
            return TryConsumeBracketWithoutOutput(text, ref index);
        }

        if (_blockCommentDepth > 0)
        {
            return TryConsumeBlockCommentWithoutOutput(text, ref index);
        }

        return false;
    }

    private bool TryStartProtectedRegionWithoutOutput(string text, ref int index)
    {
        var c = text[index];

        // Block comment: /*
        if (c == '/' && index + 1 < text.Length && text[index + 1] == '*')
        {
            _blockCommentDepth = 1;
            index += 2;
            return true;
        }

        // String literal: '
        if (c == '\'')
        {
            _inString = true;
            index++;
            return true;
        }

        // Double-quoted identifier: "
        if (c == '"')
        {
            _inDoubleQuote = true;
            index++;
            return true;
        }

        // Bracketed identifier: [
        if (c == '[')
        {
            _inBracket = true;
            index++;
            return true;
        }

        return false;
    }

    private bool TryConsumeStringWithoutOutput(string text, ref int index)
    {
        var c = text[index];
        if (c == '\'')
        {
            // Handle escaped single quotes
            if (index + 1 < text.Length && text[index + 1] == '\'')
            {
                index += 2;
                return true;
            }

            _inString = false;
        }

        index++;
        return true;
    }

    private bool TryConsumeDoubleQuoteWithoutOutput(string text, ref int index)
    {
        var c = text[index];
        if (c == '"')
        {
            _inDoubleQuote = false;
        }

        index++;
        return true;
    }

    private bool TryConsumeBracketWithoutOutput(string text, ref int index)
    {
        var c = text[index];
        if (c == ']')
        {
            // Handle escaped brackets
            if (index + 1 < text.Length && text[index + 1] == ']')
            {
                index += 2;
                return true;
            }

            _inBracket = false;
        }

        index++;
        return true;
    }

    private bool TryConsumeBlockCommentWithoutOutput(string text, ref int index)
    {
        var c = text[index];
        if (c == '/' && index + 1 < text.Length && text[index + 1] == '*')
        {
            _blockCommentDepth++;
            index += 2;
            return true;
        }

        if (c == '*' && index + 1 < text.Length && text[index + 1] == '/')
        {
            _blockCommentDepth--;
            index += 2;
            return true;
        }

        index++;
        return true;
    }
}
