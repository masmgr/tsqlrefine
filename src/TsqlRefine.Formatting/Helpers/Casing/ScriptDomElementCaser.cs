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

        var parser = ScriptDomTokenHelper.CreateParser(compatLevel);
        using var reader = new StringReader(input);
        var tokens = parser.GetTokenStream(reader, out _);
        var (previousNonTriviaIndexes, nextNonTriviaIndexes) =
            ScriptDomTokenHelper.BuildNonTriviaNeighborIndexes(tokens);

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

            var casedText = ApplyCategoryCasing(text, category, options);

            sb.Append(casedText);
        }

        return sb.ToString();
    }

    private static string ApplyCategoryCasing(
        string text,
        SqlElementCategorizer.ElementCategory category,
        FormattingOptions options)
    {
        return TryGetElementCasing(category, options, out var casing)
            ? CasingHelpers.ApplyCasing(text, casing)
            : text;
    }

    private static bool TryGetElementCasing(
        SqlElementCategorizer.ElementCategory category,
        FormattingOptions options,
        out ElementCasing casing)
    {
        switch (category)
        {
            case SqlElementCategorizer.ElementCategory.Keyword:
                casing = options.KeywordElementCasing;
                return true;
            case SqlElementCategorizer.ElementCategory.BuiltInFunction:
                casing = options.BuiltInFunctionCasing;
                return true;
            case SqlElementCategorizer.ElementCategory.DataType:
                casing = options.DataTypeCasing;
                return true;
            case SqlElementCategorizer.ElementCategory.Schema:
                casing = options.SchemaCasing;
                return true;
            case SqlElementCategorizer.ElementCategory.Table:
                casing = options.TableCasing;
                return true;
            case SqlElementCategorizer.ElementCategory.Column:
                casing = options.ColumnCasing;
                return true;
            case SqlElementCategorizer.ElementCategory.Variable:
                casing = options.VariableCasing;
                return true;
            case SqlElementCategorizer.ElementCategory.SystemTable:
                casing = options.SystemTableCasing;
                return true;
            case SqlElementCategorizer.ElementCategory.StoredProcedure:
                casing = options.StoredProcedureCasing;
                return true;
            case SqlElementCategorizer.ElementCategory.UserDefinedFunction:
                casing = options.UserDefinedFunctionCasing;
                return true;
            default:
                casing = default;
                return false;
        }
    }

    private static TSqlParserToken? GetToken(IList<TSqlParserToken> tokens, int index) =>
        index >= 0 ? tokens[index] : null;
}
