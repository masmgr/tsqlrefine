using System.Text;

namespace TsqlRefine.Formatting.Helpers;

/// <summary>
/// Tracks protected regions (strings, comments, brackets) during whitespace normalization.
/// Encapsulates state machine logic for region detection and consumption.
/// </summary>
internal sealed class ProtectedRegionTracker
{
    private bool _inString;
    private bool _inDoubleQuote;
    private bool _inBracket;
    private bool _inBlockComment;

    /// <summary>
    /// Checks if currently inside a protected region.
    /// </summary>
    /// <returns>True if inside a string, comment, or bracket; false otherwise</returns>
    public bool IsInProtectedRegion() =>
        _inString || _inBlockComment || _inDoubleQuote || _inBracket;

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
        if (!_inBlockComment)
        {
            return false;
        }

        var c = text[index];
        if (c == '*' && index + 1 < text.Length && text[index + 1] == '/')
        {
            output.Append("*/");
            _inBlockComment = false;
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
            _inBlockComment = true;
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
}
