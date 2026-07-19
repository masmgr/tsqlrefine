using System.Collections.Frozen;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.Formatting.Helpers.Registries;

namespace TsqlRefine.Formatting.Helpers.Whitespace;

/// <summary>
/// Removes whitespace between function names and their opening parentheses.
///
/// Transformations:
/// - Removes space between built-in function names and '(' (e.g., "COUNT (*)" -> "COUNT(*)")
/// - Removes space between user-defined function identifiers and '(' (e.g., "dbo.MyFunc (1)" -> "dbo.MyFunc(1)")
/// - Only applies when next non-trivia token after the function name is '('
///
/// Known limitations:
/// - Requires valid ScriptDom token stream (gracefully returns input unchanged on parse failure)
/// - CASE keyword is excluded (it is a control-flow keyword, not a function call)
/// </summary>
public static class FunctionParenSpaceNormalizer
{
    /// <summary>
    /// Keywords that appear in the built-in function registry but are actually
    /// control-flow constructs and should not have their spacing normalized.
    /// </summary>
    private static readonly FrozenSet<string> ExcludedKeywords = FrozenSet.ToFrozenSet(
        new[] { "CASE" }, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Removes whitespace between function names and opening parentheses in SQL text.
    /// </summary>
    /// <param name="input">SQL text to normalize.</param>
    /// <param name="options">Formatting options.</param>
    /// <returns>SQL text with normalized function-parenthesis spacing.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Maintainability",
        "CA1502:Avoid excessive complexity",
        Justification = "Existing token normalization loop; tracked as complexity baseline debt.")]
    public static string Normalize(string input, FormattingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        if (!options.NormalizeFunctionSpacing)
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

        var (previousNonTriviaIndexes, nextNonTriviaIndexes) =
            ScriptDomTokenHelper.BuildNonTriviaNeighborIndexes(tokens);

        // Build a set of whitespace token indexes that should be removed
        // (whitespace between a function name and its opening parenthesis)
        var skipIndexes = new HashSet<int>();

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (token.TokenType == TSqlTokenType.EndOfFile)
            {
                continue;
            }

            if (ScriptDomTokenHelper.TriviaTokenTypes.Contains(token.TokenType))
            {
                continue;
            }

            if (!IsFunctionNameToken(tokens, previousNonTriviaIndexes, i))
            {
                continue;
            }

            // Found a potential function name. Check if next non-trivia token is '('
            var nextNonTriviaIdx = nextNonTriviaIndexes[i];
            if (nextNonTriviaIdx < 0)
            {
                continue;
            }

            var nextNonTrivia = tokens[nextNonTriviaIdx];
            if (nextNonTrivia.Text != "(")
            {
                continue;
            }

            // Mark all whitespace-only trivia tokens between function name and '(' for removal
            for (var j = i + 1; j < nextNonTriviaIdx; j++)
            {
                var between = tokens[j];
                if (ScriptDomTokenHelper.TriviaTokenTypes.Contains(between.TokenType) &&
                    ScriptDomTokenHelper.IsInlineWhitespaceOnly(between.Text ?? string.Empty))
                {
                    skipIndexes.Add(j);
                }
            }
        }

        if (skipIndexes.Count == 0)
        {
            return input;
        }

        var sb = new StringBuilder(input.Length);

        for (var i = 0; i < tokens.Count; i++)
        {
            if (tokens[i].TokenType == TSqlTokenType.EndOfFile)
            {
                continue;
            }

            if (skipIndexes.Contains(i))
            {
                continue;
            }

            sb.Append(tokens[i].Text ?? string.Empty);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Determines if a token represents a function name (built-in or user-defined identifier).
    /// </summary>
    private static bool IsFunctionNameToken(
        IList<TSqlParserToken> tokens,
        int[] previousNonTriviaIndexes,
        int index)
    {
        var token = tokens[index];
        var text = token.Text;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // Exclude keywords that are in the function registry but are not functions
        if (ExcludedKeywords.Contains(text))
        {
            return false;
        }

        if (token.TokenType == TSqlTokenType.Identifier &&
            IsTableNameBeforeColumnList(tokens, previousNonTriviaIndexes, index))
        {
            return false;
        }

        // Built-in functions from registry (e.g., COUNT, SUM, ISNULL)
        if (BuiltInFunctionRegistry.IsBuiltInFunction(text))
        {
            return true;
        }

        // User-defined function identifiers: any identifier token could be a function name
        // when followed by '(' (the caller checks for the parenthesis)
        if (token.TokenType == TSqlTokenType.Identifier)
        {
            return true;
        }

        return false;
    }

    private static bool IsTableNameBeforeColumnList(
        IList<TSqlParserToken> tokens,
        int[] previousNonTriviaIndexes,
        int index)
    {
        var previousIndex = previousNonTriviaIndexes[index];

        // Walk back over a multipart table name (database.schema.table).
        while (previousIndex >= 0 && tokens[previousIndex].Text == ".")
        {
            var qualifierIndex = previousNonTriviaIndexes[previousIndex];
            if (qualifierIndex < 0)
            {
                return false;
            }

            previousIndex = previousNonTriviaIndexes[qualifierIndex];
        }

        var previousText = previousIndex >= 0 ? tokens[previousIndex].Text : null;
        return previousText is not null &&
               (previousText.Equals("INTO", StringComparison.OrdinalIgnoreCase) ||
                previousText.Equals("TABLE", StringComparison.OrdinalIgnoreCase) ||
                previousText.Equals("FROM", StringComparison.OrdinalIgnoreCase) ||
                previousText.Equals("JOIN", StringComparison.OrdinalIgnoreCase) ||
                previousText.Equals("UPDATE", StringComparison.OrdinalIgnoreCase));
    }
}
