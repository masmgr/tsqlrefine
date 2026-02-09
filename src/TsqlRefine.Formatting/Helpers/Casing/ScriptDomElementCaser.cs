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
        var previousNonTriviaIndexes = ScriptDomTokenHelper.BuildPreviousNonTriviaIndexes(tokens);
        var nextNonTriviaIndexes = ScriptDomTokenHelper.BuildNextNonTriviaIndexes(tokens);

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

    private static TSqlParserToken? GetToken(IList<TSqlParserToken> tokens, int index) =>
        index >= 0 ? tokens[index] : null;
}
