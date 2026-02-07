using System.Collections.Frozen;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Formatting.Helpers.Casing;

/// <summary>
/// Applies granular casing transformations to SQL elements using ScriptDom token stream.
/// Supports independent casing control for keywords, functions, data types, schemas,
/// tables, columns, and variables.
/// </summary>
public static class ScriptDomElementCaser
{
    private static readonly FrozenSet<TSqlTokenType> TriviaTokenTypes = BuildTriviaTokenTypes();

    /// <summary>
    /// Applies granular element casing to SQL text.
    /// </summary>
    /// <param name="input">The SQL text to transform</param>
    /// <param name="options">Formatting options containing casing settings</param>
    /// <param name="compatLevel">SQL Server compatibility level (100-160). Defaults to 150 (SQL Server 2019)</param>
    /// <returns>SQL text with casing applied</returns>
    public static string Apply(string input, FormattingOptions options, int compatLevel = 150)
    {
        ArgumentNullException.ThrowIfNull(options);

        var parser = CreateParser(compatLevel);
        using var reader = new StringReader(input);
        var tokens = parser.GetTokenStream(reader, out _);
        var previousNonTriviaIndexes = BuildPreviousNonTriviaIndexes(tokens);
        var nextNonTriviaIndexes = BuildNextNonTriviaIndexes(tokens);

        var sb = new StringBuilder(input.Length + 16);
        var context = new CasingContext();

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (token.TokenType == TSqlTokenType.EndOfFile)
            {
                continue;
            }

            var text = token.Text ?? string.Empty;

            // Get surrounding tokens for categorization
            var previousToken = GetToken(tokens, previousNonTriviaIndexes[i]);
            var nextToken = GetToken(tokens, nextNonTriviaIndexes[i]);

            // Categorize token with context tracking
            var category = SqlElementCategorizer.Categorize(token, previousToken, nextToken, context);

            // Apply casing based on category
            var casedText = category switch
            {
                SqlElementCategorizer.ElementCategory.Keyword =>
                    CasingHelpers.ApplyCasing(text, options.KeywordElementCasing),

                SqlElementCategorizer.ElementCategory.BuiltInFunction =>
                    CasingHelpers.ApplyCasing(text, options.BuiltInFunctionCasing),

                SqlElementCategorizer.ElementCategory.DataType =>
                    CasingHelpers.ApplyCasing(text, options.DataTypeCasing),

                SqlElementCategorizer.ElementCategory.Schema =>
                    CasingHelpers.ApplyCasing(text, options.SchemaCasing),

                SqlElementCategorizer.ElementCategory.Table =>
                    CasingHelpers.ApplyCasing(text, options.TableCasing),

                SqlElementCategorizer.ElementCategory.Column =>
                    CasingHelpers.ApplyCasing(text, options.ColumnCasing),

                SqlElementCategorizer.ElementCategory.Variable =>
                    CasingHelpers.ApplyCasing(text, options.VariableCasing),

                SqlElementCategorizer.ElementCategory.SystemTable =>
                    CasingHelpers.ApplyCasing(text, options.SystemTableCasing),

                SqlElementCategorizer.ElementCategory.StoredProcedure =>
                    CasingHelpers.ApplyCasing(text, options.StoredProcedureCasing),

                SqlElementCategorizer.ElementCategory.UserDefinedFunction =>
                    CasingHelpers.ApplyCasing(text, options.UserDefinedFunctionCasing),

                _ => text
            };

            sb.Append(casedText);
        }

        return sb.ToString();
    }

    private static int[] BuildPreviousNonTriviaIndexes(IList<TSqlParserToken> tokens)
    {
        var indexes = new int[tokens.Count];
        var previousIndex = -1;

        for (var i = 0; i < tokens.Count; i++)
        {
            indexes[i] = previousIndex;
            if (!IsTrivia(tokens[i]))
            {
                previousIndex = i;
            }
        }

        return indexes;
    }

    private static int[] BuildNextNonTriviaIndexes(IList<TSqlParserToken> tokens)
    {
        var indexes = new int[tokens.Count];
        var nextIndex = -1;

        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            indexes[i] = nextIndex;
            if (!IsTrivia(tokens[i]))
            {
                nextIndex = i;
            }
        }

        return indexes;
    }

    private static TSqlParserToken? GetToken(IList<TSqlParserToken> tokens, int index) =>
        index >= 0 ? tokens[index] : null;

    /// <summary>
    /// Checks if a token is trivia (whitespace or comment).
    /// </summary>
    private static bool IsTrivia(TSqlParserToken token) => TriviaTokenTypes.Contains(token.TokenType);

    /// <summary>
    /// Creates a T-SQL parser for the specified compatibility level.
    /// </summary>
    /// <param name="compatLevel">SQL Server compatibility level (100-160)</param>
    /// <returns>TSqlParser instance for the specified version</returns>
    private static TSqlParser CreateParser(int compatLevel) =>
        compatLevel switch
        {
            >= 160 => new TSql160Parser(initialQuotedIdentifiers: true),
            >= 150 => new TSql150Parser(initialQuotedIdentifiers: true),
            >= 140 => new TSql140Parser(initialQuotedIdentifiers: true),
            >= 130 => new TSql130Parser(initialQuotedIdentifiers: true),
            >= 120 => new TSql120Parser(initialQuotedIdentifiers: true),
            >= 110 => new TSql110Parser(initialQuotedIdentifiers: true),
            _ => new TSql100Parser(initialQuotedIdentifiers: true)
        };

    private static FrozenSet<TSqlTokenType> BuildTriviaTokenTypes()
    {
        var triviaTypes = new HashSet<TSqlTokenType>();
        foreach (var tokenType in Enum.GetValues<TSqlTokenType>())
        {
            var name = tokenType.ToString();
            if (name.Contains("WhiteSpace", StringComparison.Ordinal) ||
                name.Contains("Whitespace", StringComparison.Ordinal) ||
                name.Contains("Comment", StringComparison.Ordinal))
            {
                triviaTypes.Add(tokenType);
            }
        }

        return triviaTypes.ToFrozenSet();
    }
}
