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
        var keywordCased = ScriptDomKeywordCaser.Apply(sql, options.KeywordCasing, options.IdentifierCasing, compatLevel: 150);
        var whitespaceNormalized = WhitespaceNormalizer.Normalize(keywordCased, options);

        // Apply comma style if not default trailing
        if (options.CommaStyle == CommaStyle.Leading)
        {
            whitespaceNormalized = CommaStyleTransformer.ToLeadingCommas(whitespaceNormalized);
        }

        return whitespaceNormalized;
    }
}
