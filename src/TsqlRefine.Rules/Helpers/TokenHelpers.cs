using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers;

/// <summary>
/// Helper utilities for working with token streams.
/// </summary>
public static class TokenHelpers
{
    /// <summary>
    /// Checks if a token is a keyword matching the specified text (case-insensitive).
    /// </summary>
    /// <param name="token">The token to check.</param>
    /// <param name="keyword">The keyword to match against.</param>
    /// <returns>True if the token text matches the keyword (case-insensitive), false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when token or keyword is null.</exception>
    public static bool IsKeyword(Token token, string keyword)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentNullException.ThrowIfNull(keyword);
        return token.Text.Equals(keyword, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a token is trivia (whitespace or comment).
    /// </summary>
    /// <param name="token">The token to check.</param>
    /// <returns>True if the token is whitespace or a comment, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when token is null.</exception>
    public static bool IsTrivia(Token token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var text = token.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        return text.StartsWith("--", StringComparison.Ordinal) ||
               text.StartsWith("/*", StringComparison.Ordinal);
    }

    /// <summary>
    /// Checks if a token at the given index is prefixed by a dot token (ignoring trivia).
    /// </summary>
    /// <param name="tokens">The token list to search.</param>
    /// <param name="index">The index of the token to check.</param>
    /// <returns>True if the previous non-trivia token is a dot, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when tokens is null.</exception>
    public static bool IsPrefixedByDot(IReadOnlyList<Token> tokens, int index)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        for (var i = index - 1; i >= 0; i--)
        {
            if (IsTrivia(tokens[i]))
            {
                continue;
            }

            return tokens[i].Text == ".";
        }

        return false;
    }

    /// <summary>
    /// Calculates the end position of a token, accounting for multi-line tokens.
    /// Handles \r\n, \r, and \n line endings.
    /// </summary>
    /// <param name="token">The token to calculate the end position for.</param>
    /// <returns>The end position of the token.</returns>
    /// <exception cref="ArgumentNullException">Thrown when token is null.</exception>
    public static Position GetTokenEnd(Token token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var text = token.Text ?? string.Empty;
        var start = token.Start;
        if (text.Length == 0)
        {
            return start;
        }

        // Fast path: most tokens are single-line.
        if (text.AsSpan().IndexOfAny('\r', '\n') < 0)
        {
            return new Position(start.Line, start.Character + token.Length);
        }

        var line = start.Line;
        var character = start.Character;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (ch == '\r')
            {
                if (i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                line++;
                character = 0;
                continue;
            }

            if (ch == '\n')
            {
                line++;
                character = 0;
                continue;
            }

            character++;
        }

        return new Position(line, character);
    }

    /// <summary>
    /// Checks if a token is a SQL keyword based on its TokenType.
    /// Uses the same heuristic as the formatter: excludes tokens whose type name
    /// contains "Identifier", "Comment", "WhiteSpace", "Variable", or "Literal".
    /// </summary>
    /// <param name="token">The token to check.</param>
    /// <returns>True if the token is a keyword, false otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when token is null.</exception>
    public static bool IsLikelyKeyword(Token token)
    {
        ArgumentNullException.ThrowIfNull(token);

        var text = token.Text;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // Must be a word (starts with letter, contains only letters/digits/_)
        if (!char.IsLetter(text[0]))
        {
            return false;
        }

        for (var i = 1; i < text.Length; i++)
        {
            var c = text[i];
            if (!char.IsLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        // Check TokenType if available
        if (token.TokenType is not null)
        {
            // Exclude non-keyword token types (same logic as SqlFormatter)
            var typeName = token.TokenType;
            if (typeName.Contains("Identifier", StringComparison.Ordinal) ||
                typeName.Contains("Comment", StringComparison.Ordinal) ||
                typeName.Contains("WhiteSpace", StringComparison.Ordinal) ||
                typeName.Contains("Whitespace", StringComparison.Ordinal) ||
                typeName.Contains("Variable", StringComparison.Ordinal) ||
                typeName.Contains("Literal", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Skips trivia tokens starting from the given index and returns the index of the next non-trivia token.
    /// </summary>
    /// <param name="tokens">The token list to search.</param>
    /// <param name="startIndex">The index to start skipping from.</param>
    /// <returns>The index of the next non-trivia token, or tokens.Count if no non-trivia token is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when tokens is null.</exception>
    public static int SkipTrivia(IReadOnlyList<Token> tokens, int startIndex)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        var index = startIndex;
        while (index < tokens.Count && IsTrivia(tokens[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>
    /// Creates a Range from start and end token indices in the token list.
    /// The range spans from the start of the start token to the end of the end token.
    /// </summary>
    /// <param name="tokens">The token list.</param>
    /// <param name="startIndex">The index of the start token.</param>
    /// <param name="endIndex">The index of the end token.</param>
    /// <returns>A Range spanning the specified tokens.</returns>
    /// <exception cref="ArgumentNullException">Thrown when tokens is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when indices are invalid.</exception>
    public static TsqlRefine.PluginSdk.Range GetTokenRange(
        IReadOnlyList<Token> tokens,
        int startIndex,
        int endIndex)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        if (startIndex < 0 || startIndex >= tokens.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(startIndex));
        }

        if (endIndex < 0 || endIndex >= tokens.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(endIndex));
        }

        if (startIndex > endIndex)
        {
            throw new ArgumentException("startIndex must be less than or equal to endIndex");
        }

        var start = tokens[startIndex].Start;
        var end = GetTokenEnd(tokens[endIndex]);

        return new TsqlRefine.PluginSdk.Range(start, end);
    }

    /// <summary>
    /// Gets the index of the previous non-trivia token before the specified index.
    /// </summary>
    /// <param name="tokens">The token list to search.</param>
    /// <param name="index">The index to start searching backwards from (exclusive).</param>
    /// <returns>The index of the previous non-trivia token, or -1 if not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when tokens is null.</exception>
    public static int GetPreviousNonTriviaIndex(IReadOnlyList<Token> tokens, int index)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        for (var i = index - 1; i >= 0; i--)
        {
            if (!IsTrivia(tokens[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Gets the index of the next non-trivia token after the specified index.
    /// </summary>
    /// <param name="tokens">The token list to search.</param>
    /// <param name="index">The index to start searching forward from (exclusive).</param>
    /// <returns>The index of the next non-trivia token, or -1 if not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when tokens is null.</exception>
    public static int GetNextNonTriviaIndex(IReadOnlyList<Token> tokens, int index)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        for (var i = index + 1; i < tokens.Count; i++)
        {
            if (!IsTrivia(tokens[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Finds a token that exactly matches the specified range.
    /// </summary>
    /// <param name="tokens">The token list to search.</param>
    /// <param name="range">The range to match.</param>
    /// <returns>The matching token, or null if not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when tokens or range is null.</exception>
    public static Token? FindTokenByRange(
        IReadOnlyList<Token> tokens,
        TsqlRefine.PluginSdk.Range range)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(range);

        foreach (var token in tokens)
        {
            if (IsTrivia(token))
            {
                continue;
            }

            if (token.Start != range.Start)
            {
                continue;
            }

            var end = GetTokenEnd(token);
            if (end == range.End)
            {
                return token;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the index of a token that exactly matches the specified range.
    /// </summary>
    /// <param name="tokens">The token list to search.</param>
    /// <param name="range">The range to match.</param>
    /// <returns>The index of the matching token, or -1 if not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when tokens or range is null.</exception>
    public static int FindTokenIndexByRange(
        IReadOnlyList<Token> tokens,
        TsqlRefine.PluginSdk.Range range)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(range);

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (IsTrivia(token))
            {
                continue;
            }

            if (token.Start != range.Start)
            {
                continue;
            }

            var end = GetTokenEnd(token);
            if (end == range.End)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Checks if a position is within a range (inclusive).
    /// </summary>
    /// <param name="position">The position to check.</param>
    /// <param name="range">The range to check against.</param>
    /// <returns>True if the position is within the range.</returns>
    /// <exception cref="ArgumentNullException">Thrown when position or range is null.</exception>
    public static bool IsPositionWithinRange(
        Position position,
        TsqlRefine.PluginSdk.Range range)
    {
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(range);

        if (position.Line < range.Start.Line || position.Line > range.End.Line)
        {
            return false;
        }

        if (position.Line == range.Start.Line && position.Character < range.Start.Character)
        {
            return false;
        }

        if (position.Line == range.End.Line && position.Character > range.End.Character)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Applies the casing pattern from the original keyword to the replacement keyword.
    /// If the original is all uppercase, returns uppercase. If all lowercase, returns lowercase.
    /// Otherwise, returns uppercase as the default.
    /// </summary>
    /// <param name="replacementKeyword">The keyword to apply casing to.</param>
    /// <param name="originalKeyword">The original keyword to derive casing from.</param>
    /// <returns>The replacement keyword with appropriate casing.</returns>
    /// <exception cref="ArgumentNullException">Thrown when replacementKeyword is null.</exception>
    public static string ApplyKeywordCasing(string replacementKeyword, string? originalKeyword)
    {
        ArgumentNullException.ThrowIfNull(replacementKeyword);

        if (string.IsNullOrEmpty(originalKeyword))
        {
            return replacementKeyword;
        }

        if (originalKeyword.All(c => !char.IsLetter(c) || char.IsUpper(c)))
        {
            return replacementKeyword.ToUpperInvariant();
        }

        if (originalKeyword.All(c => !char.IsLetter(c) || char.IsLower(c)))
        {
            return replacementKeyword.ToLowerInvariant();
        }

        return replacementKeyword.ToUpperInvariant();
    }
}
