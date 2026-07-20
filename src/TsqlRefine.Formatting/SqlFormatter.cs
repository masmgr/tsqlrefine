using TsqlRefine.Formatting.Helpers.Casing;
using TsqlRefine.Formatting.Helpers.Transformation;
using TsqlRefine.Formatting.Helpers.Whitespace;

namespace TsqlRefine.Formatting;

/// <summary>
/// Minimal SQL formatter that applies granular element casing, whitespace normalization,
/// inline spacing normalization, and comma style transformations while preserving comments,
/// string literals, and code structure.
///
/// Formatting pipeline:
/// 1. Keyword space normalization (KeywordSpaceNormalizer)
/// 2. Granular element casing (ScriptDomElementCaser)
/// 3. Whitespace normalization (WhitespaceNormalizer)
/// 4. Blank line normalization (BlankLineNormalizer)
/// 5. Inline spacing normalization (InlineSpaceNormalizer)
/// 6. Function-parenthesis spacing normalization (FunctionParenSpaceNormalizer)
/// 7. Operator spacing normalization (OperatorSpaceNormalizer)
/// 8. Comma style transformation (CommaStyleTransformer, optional)
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

        // Strip standalone CR characters (\r not followed by \n) before any normalizer runs
        sql = LineEndingHelpers.StripStandaloneCr(sql);

        // Apply keyword space normalization (collapse multi-space between compound keyword pairs)
        var keywordNormalized = KeywordSpaceNormalizer.Normalize(sql, options);

        // Apply granular element-based casing
        var casedSql = ScriptDomElementCaser.Apply(keywordNormalized, options);

        var whitespaceNormalized = WhitespaceNormalizer.Normalize(casedSql, options);

        // Apply blank line normalization (consecutive blank line limiting and leading blank trimming)
        var blankLineNormalized = BlankLineNormalizer.Normalize(whitespaceNormalized, options);

        // Apply inline spacing normalization
        var inlineNormalized = InlineSpaceNormalizer.Normalize(blankLineNormalized, options);

        // Apply function-parenthesis spacing normalization
        var functionNormalized = FunctionParenSpaceNormalizer.Normalize(inlineNormalized, options);

        // Apply operator spacing normalization
        var operatorNormalized = OperatorSpaceNormalizer.Normalize(functionNormalized, options);

        // Apply comma style transformation
        if (options.CommaStyle == CommaStyle.Leading)
        {
            operatorNormalized = CommaStyleTransformer.ToLeadingCommas(operatorNormalized);
        }
        else if (options.CommaStyle == CommaStyle.Trailing)
        {
            operatorNormalized = CommaStyleTransformer.ToTrailingCommas(operatorNormalized);
        }

        return operatorNormalized;
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
