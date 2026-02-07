using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Diagnostics;

/// <summary>
/// Helper utilities for implementing IRule interface.
/// </summary>
public static class RuleHelpers
{
    /// <summary>
    /// Returns an empty fix collection for rules that don't support auto-fixing.
    /// Validates parameters to ensure proper null checking.
    /// </summary>
    /// <param name="context">The rule context.</param>
    /// <param name="diagnostic">The diagnostic.</param>
    /// <returns>An empty collection of fixes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when context or diagnostic is null.</exception>
    public static IEnumerable<Fix> NoFixes(RuleContext context, Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostic);
        return Array.Empty<Fix>();
    }

    /// <summary>
    /// Creates a diagnostic with the specified parameters.
    /// This factory method ensures consistent DiagnosticData construction.
    /// </summary>
    /// <param name="range">The source code range of the diagnostic.</param>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="code">The diagnostic code (rule ID).</param>
    /// <param name="category">The diagnostic category.</param>
    /// <param name="fixable">Whether the diagnostic is fixable.</param>
    /// <returns>A new Diagnostic instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public static Diagnostic CreateDiagnostic(
        TsqlRefine.PluginSdk.Range range,
        string message,
        string code,
        string category,
        bool fixable)
    {
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(category);

        return new Diagnostic(
            Range: range,
            Message: message,
            Severity: null,
            Code: code,
            Data: new DiagnosticData(code, category, fixable)
        );
    }

    /// <summary>
    /// Creates a diagnostic from token indices with automatic range calculation.
    /// This factory method simplifies diagnostic creation in token-based rules.
    /// </summary>
    /// <param name="tokens">The token list.</param>
    /// <param name="startIndex">The index of the start token.</param>
    /// <param name="endIndex">The index of the end token.</param>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="code">The diagnostic code (rule ID).</param>
    /// <param name="category">The diagnostic category.</param>
    /// <param name="fixable">Whether the diagnostic is fixable.</param>
    /// <returns>A new Diagnostic instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when indices are invalid.</exception>
    public static Diagnostic CreateDiagnosticFromTokens(
        IReadOnlyList<Token> tokens,
        int startIndex,
        int endIndex,
        string message,
        string code,
        string category,
        bool fixable)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(category);

        var range = TokenHelpers.GetTokenRange(tokens, startIndex, endIndex);

        return new Diagnostic(
            Range: range,
            Message: message,
            Severity: null,
            Code: code,
            Data: new DiagnosticData(code, category, fixable)
        );
    }

    /// <summary>
    /// Validates common preconditions for GetFixes and returns whether the fix can proceed.
    /// This helper reduces boilerplate in rule implementations.
    /// </summary>
    /// <param name="context">The rule context.</param>
    /// <param name="diagnostic">The diagnostic to fix.</param>
    /// <param name="expectedRuleId">The expected rule ID for this fix.</param>
    /// <returns>True if the fix can proceed, false if it should return early.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public static bool CanProvideFix(
        RuleContext context,
        Diagnostic diagnostic,
        string expectedRuleId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostic);
        ArgumentNullException.ThrowIfNull(expectedRuleId);

        if (!string.Equals(diagnostic.Code, expectedRuleId, StringComparison.Ordinal))
        {
            return false;
        }

        return diagnostic.Data?.Fixable is true;
    }

    /// <summary>
    /// Creates a Fix that inserts text at a position (zero-width range).
    /// </summary>
    /// <param name="title">The fix title displayed to the user.</param>
    /// <param name="position">The position to insert at.</param>
    /// <param name="text">The text to insert.</param>
    /// <returns>A new Fix instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public static Fix CreateInsertFix(
        string title,
        Position position,
        string text)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(position);
        ArgumentNullException.ThrowIfNull(text);

        var insertRange = new TsqlRefine.PluginSdk.Range(position, position);
        return new Fix(title, [new TextEdit(insertRange, text)]);
    }

    /// <summary>
    /// Creates a Fix that replaces text at a range.
    /// </summary>
    /// <param name="title">The fix title displayed to the user.</param>
    /// <param name="range">The range to replace.</param>
    /// <param name="newText">The replacement text.</param>
    /// <returns>A new Fix instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public static Fix CreateReplaceFix(
        string title,
        TsqlRefine.PluginSdk.Range range,
        string newText)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(range);
        ArgumentNullException.ThrowIfNull(newText);

        return new Fix(title, [new TextEdit(range, newText)]);
    }
}
