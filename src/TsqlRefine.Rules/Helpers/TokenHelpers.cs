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
        var line = token.Start.Line;
        var character = token.Start.Character;

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
}
