using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Tokens;

/// <summary>
/// Helper utilities for raw text analysis of SQL code.
/// </summary>
public static class TextAnalysisHelpers
{
    /// <summary>
    /// Splits SQL text into lines, handling all common line ending formats (CRLF, CR, LF).
    /// </summary>
    /// <param name="sql">The SQL text to split.</param>
    /// <returns>An array of lines, or an empty array if the input is null or empty.</returns>
    public static string[] SplitSqlLines(string? sql)
    {
        if (string.IsNullOrEmpty(sql))
        {
            return Array.Empty<string>();
        }

        return sql.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    }

    /// <summary>
    /// Creates a diagnostic for a specific line in the SQL text.
    /// The range spans from the start of the line to the specified length.
    /// </summary>
    /// <param name="lineNumber">The zero-based line number.</param>
    /// <param name="lineLength">The length of the line (number of characters).</param>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="code">The diagnostic code (rule ID).</param>
    /// <param name="category">The diagnostic category.</param>
    /// <param name="fixable">Whether the diagnostic is fixable.</param>
    /// <returns>A new Diagnostic instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any string parameter is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when lineNumber or lineLength is negative.</exception>
    public static Diagnostic CreateLineRangeDiagnostic(
        int lineNumber,
        int lineLength,
        string message,
        string code,
        string category,
        bool fixable)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(category);

        if (lineNumber < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber), "Line number must be non-negative");
        }

        if (lineLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lineLength), "Line length must be non-negative");
        }

        return new Diagnostic(
            Range: new TsqlRefine.PluginSdk.Range(
                new Position(lineNumber, 0),
                new Position(lineNumber, lineLength)
            ),
            Message: message,
            Severity: null,
            Code: code,
            Data: new DiagnosticData(code, category, fixable)
        );
    }
}
