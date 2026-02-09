using System.Collections.Frozen;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Formatting.Helpers.Whitespace;

/// <summary>
/// Normalizes spacing between compound keyword pairs while preserving
/// all other whitespace patterns (alignment, indentation, user formatting).
///
/// Transformations:
/// - Collapses multi-space/tab runs between known keyword pairs to single space
///   (e.g., "LEFT   OUTER   JOIN" -> "LEFT OUTER JOIN")
///   (e.g., "GROUP   BY" -> "GROUP BY")
///   (e.g., "IS   NOT   NULL" -> "IS NOT NULL")
/// - Only normalizes within a predefined set of safe compound keyword pairs
/// - Preserves all non-keyword spacing (user alignment, identifier spacing)
/// - Preserves line breaks (only collapses same-line whitespace)
///
/// Known limitations:
/// - Requires valid ScriptDom token stream (gracefully returns input unchanged on parse failure)
/// - Only normalizes predefined keyword pairs (not all keyword-keyword whitespace)
/// </summary>
public static class KeywordSpaceNormalizer
{
    /// <summary>
    /// Known compound keyword pairs that are safe to normalize spacing between.
    /// Format: "KEYWORD1 KEYWORD2" (uppercase, space-separated).
    /// </summary>
    private static readonly FrozenSet<string> CompoundKeywordPairs = FrozenSet.ToFrozenSet(new[]
    {
        // JOIN variants
        "LEFT OUTER", "RIGHT OUTER", "FULL OUTER",
        "LEFT JOIN", "RIGHT JOIN", "FULL JOIN", "INNER JOIN", "CROSS JOIN", "OUTER JOIN",
        "CROSS APPLY", "OUTER APPLY",

        // BY clauses
        "GROUP BY", "ORDER BY", "PARTITION BY",

        // Negation
        "IS NOT", "NOT NULL", "NOT IN", "NOT EXISTS", "NOT BETWEEN", "NOT LIKE",

        // DML
        "INSERT INTO", "DELETE FROM",

        // Set operations
        "UNION ALL", "EXCEPT ALL", "INTERSECT ALL",

        // DDL: CREATE
        "CREATE TABLE", "CREATE VIEW", "CREATE PROCEDURE", "CREATE FUNCTION",
        "CREATE INDEX", "CREATE TRIGGER", "CREATE SCHEMA", "CREATE DATABASE", "CREATE TYPE",

        // DDL: ALTER
        "ALTER TABLE", "ALTER VIEW", "ALTER PROCEDURE", "ALTER FUNCTION",
        "ALTER INDEX", "ALTER SCHEMA", "ALTER DATABASE",

        // DDL: DROP
        "DROP TABLE", "DROP VIEW", "DROP PROCEDURE", "DROP FUNCTION",
        "DROP INDEX", "DROP TRIGGER", "DROP SCHEMA", "DROP DATABASE",

        // Constraints
        "PRIMARY KEY", "FOREIGN KEY",

        // Transaction
        "BEGIN TRANSACTION", "BEGIN TRAN", "COMMIT TRANSACTION", "COMMIT TRAN",
        "ROLLBACK TRANSACTION", "ROLLBACK TRAN",

        // Control flow
        "BEGIN TRY", "END TRY", "BEGIN CATCH", "END CATCH",

        // Conditional
        "IF EXISTS", "IF NOT"
    }, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Normalizes spacing between compound keyword pairs in SQL text.
    /// </summary>
    /// <param name="input">SQL text to normalize</param>
    /// <param name="options">Formatting options</param>
    /// <returns>SQL text with normalized keyword spacing</returns>
    public static string Normalize(string input, FormattingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        if (!options.NormalizeKeywordSpacing)
        {
            return input;
        }

        var parser = ScriptDomTokenHelper.CreateParser(options.CompatLevel);
        using var reader = new StringReader(input);
        var tokens = parser.GetTokenStream(reader, out _);

        if (tokens is null || tokens.Count == 0)
        {
            return input;
        }

        var previousNonTriviaIndexes = ScriptDomTokenHelper.BuildPreviousNonTriviaIndexes(tokens);
        var nextNonTriviaIndexes = ScriptDomTokenHelper.BuildNextNonTriviaIndexes(tokens);

        var sb = new StringBuilder(input.Length);

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (token.TokenType == TSqlTokenType.EndOfFile)
            {
                continue;
            }

            var text = token.Text ?? string.Empty;

            // Check if this is a whitespace token eligible for normalization
            if (ScriptDomTokenHelper.TriviaTokenTypes.Contains(token.TokenType) &&
                IsInlineWhitespaceOnly(text))
            {
                var prevIdx = previousNonTriviaIndexes[i];
                var nextIdx = nextNonTriviaIndexes[i];

                if (prevIdx >= 0 && nextIdx >= 0 &&
                    !ScriptDomTokenHelper.TriviaTokenTypes.Contains(tokens[prevIdx].TokenType) &&
                    !ScriptDomTokenHelper.TriviaTokenTypes.Contains(tokens[nextIdx].TokenType))
                {
                    var prevText = tokens[prevIdx].Text ?? string.Empty;
                    var nextText = tokens[nextIdx].Text ?? string.Empty;
                    var pair = $"{prevText} {nextText}";

                    if (CompoundKeywordPairs.Contains(pair))
                    {
                        sb.Append(' ');
                        continue;
                    }
                }
            }

            sb.Append(text);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Checks if whitespace text contains only spaces and tabs (no line breaks).
    /// </summary>
    private static bool IsInlineWhitespaceOnly(string text)
    {
        foreach (var c in text)
        {
            if (c is not (' ' or '\t'))
            {
                return false;
            }
        }

        return text.Length > 0;
    }
}
