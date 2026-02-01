using TsqlRefine.Formatting.Helpers;

namespace TsqlRefine.Formatting;

/// <summary>
/// Minimal SQL formatter that applies granular element casing, whitespace normalization,
/// inline spacing normalization, and comma style transformations while preserving comments,
/// string literals, and code structure.
///
/// Formatting pipeline:
/// 1. Granular element casing (ScriptDomElementCaser)
/// 2. Whitespace normalization (WhitespaceNormalizer)
/// 3. Inline spacing normalization (InlineSpaceNormalizer)
/// 4. Comma style transformation (CommaStyleTransformer, optional)
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
        options ??= new FormattingOptions();

        if (string.IsNullOrEmpty(sql))
        {
            // For empty input, apply InsertFinalNewline if configured
            // Use CRLF as fallback for Auto mode (Windows-preferred)
            return options.InsertFinalNewline
                ? GetLineEndingString(options.LineEnding, fallback: "\r\n")
                : string.Empty;
        }

        // Apply granular element-based casing
        var casedSql = ScriptDomElementCaser.Apply(sql, options, compatLevel: options.CompatLevel);

        var whitespaceNormalized = WhitespaceNormalizer.Normalize(casedSql, options);

        // Apply inline spacing normalization
        var inlineNormalized = InlineSpaceNormalizer.Normalize(whitespaceNormalized, options);

        // Apply comma style if not default trailing
        if (options.CommaStyle == CommaStyle.Leading)
        {
            inlineNormalized = CommaStyleTransformer.ToLeadingCommas(inlineNormalized);
        }

        return inlineNormalized;
    }

    private static string GetLineEndingString(LineEnding setting, string fallback)
    {
        return setting switch
        {
            LineEnding.CrLf => "\r\n",
            LineEnding.Lf => "\n",
            LineEnding.Auto => fallback,
            _ => fallback
        };
    }
}
