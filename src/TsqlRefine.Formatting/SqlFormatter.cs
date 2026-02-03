using Microsoft.SqlServer.TransactSql.ScriptDom;
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
        => Format(sql, options, ast: null);

    /// <summary>
    /// Formats SQL code according to the specified options, using AST for more accurate formatting.
    /// </summary>
    /// <param name="sql">The SQL code to format. Can be null or empty.</param>
    /// <param name="options">Formatting options. If null, defaults are used.</param>
    /// <param name="ast">Optional parsed AST fragment for accurate operator context detection.</param>
    /// <returns>The formatted SQL code, or empty string if input is null/empty.</returns>
    public static string Format(string sql, FormattingOptions? options, TSqlFragment? ast)
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

        // Build AST position map for operator context detection (if AST provided)
        var positionMap = AstPositionMap.Build(ast);

        // Apply granular element-based casing
        var casedSql = ScriptDomElementCaser.Apply(sql, options, compatLevel: options.CompatLevel);

        var whitespaceNormalized = WhitespaceNormalizer.Normalize(casedSql, options);

        // Apply inline spacing normalization
        var inlineNormalized = InlineSpaceNormalizer.Normalize(whitespaceNormalized, options);

        // Apply operator spacing normalization with AST context
        var operatorNormalized = OperatorSpaceNormalizer.Normalize(inlineNormalized, options, positionMap);

        // Apply comma style if not default trailing
        if (options.CommaStyle == CommaStyle.Leading)
        {
            operatorNormalized = CommaStyleTransformer.ToLeadingCommas(operatorNormalized);
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
