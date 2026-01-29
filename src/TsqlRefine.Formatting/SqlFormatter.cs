using TsqlRefine.Formatting.Helpers;

namespace TsqlRefine.Formatting;

/// <summary>
/// Minimal SQL formatter that applies keyword casing, whitespace normalization,
/// and comma style transformations while preserving comments, string literals,
/// and code structure.
///
/// Formatting pipeline:
/// 1. Keyword/Identifier casing (ScriptDomKeywordCaser)
/// 2. Whitespace normalization (WhitespaceNormalizer)
/// 3. Comma style transformation (CommaStyleTransformer, optional)
/// </summary>
public static class SqlFormatter
{
    /// <summary>
    /// Formats SQL code according to the specified options.
    /// </summary>
    /// <param name="sql">The SQL code to format. Can be null or empty.</param>
    /// <param name="options">Formatting options. If null, defaults are used.</param>
    /// <returns>The formatted SQL code, or empty string if input is null/empty.</returns>
    public static string Format(string sql, FormattingOptions? options = null)
    {
        if (string.IsNullOrEmpty(sql))
        {
            return string.Empty;
        }

        options ??= new FormattingOptions();

        // Check if granular element casing is explicitly enabled (any property set)
        var useGranularCasing = options.KeywordElementCasing.HasValue ||
                               options.BuiltInFunctionCasing.HasValue ||
                               options.DataTypeCasing.HasValue ||
                               options.SchemaCasing.HasValue ||
                               options.TableCasing.HasValue ||
                               options.ColumnCasing.HasValue ||
                               options.VariableCasing.HasValue;

        string casedSql;
        if (useGranularCasing)
        {
            // Use granular element-based casing with recommended defaults:
            // Keywords: UPPER, Functions: UPPER, Types: lower, Schema: lower,
            // Table: UPPER, Column: UPPER, Variables: lower
            var effectiveOptions = new FormattingOptions
            {
                KeywordElementCasing = options.KeywordElementCasing ?? ElementCasing.Upper,
                BuiltInFunctionCasing = options.BuiltInFunctionCasing ?? ElementCasing.Upper,
                DataTypeCasing = options.DataTypeCasing ?? ElementCasing.Lower,
                SchemaCasing = options.SchemaCasing ?? ElementCasing.Lower,
                TableCasing = options.TableCasing ?? ElementCasing.Upper,
                ColumnCasing = options.ColumnCasing ?? ElementCasing.Upper,
                VariableCasing = options.VariableCasing ?? ElementCasing.Lower,
                // Copy other settings
                IndentStyle = options.IndentStyle,
                IndentSize = options.IndentSize,
                CommaStyle = options.CommaStyle,
                MaxLineLength = options.MaxLineLength,
                InsertFinalNewline = options.InsertFinalNewline,
                TrimTrailingWhitespace = options.TrimTrailingWhitespace
            };
            casedSql = ScriptDomElementCaser.Apply(sql, effectiveOptions, compatLevel: 150);
        }
        else
        {
            // Use legacy keyword/identifier casing for backward compatibility
            casedSql = ScriptDomKeywordCaser.Apply(sql, options.KeywordCasing, options.IdentifierCasing, compatLevel: 150);
        }

        var whitespaceNormalized = WhitespaceNormalizer.Normalize(casedSql, options);

        // Apply comma style if not default trailing
        if (options.CommaStyle == CommaStyle.Leading)
        {
            whitespaceNormalized = CommaStyleTransformer.ToLeadingCommas(whitespaceNormalized);
        }

        return whitespaceNormalized;
    }
}
