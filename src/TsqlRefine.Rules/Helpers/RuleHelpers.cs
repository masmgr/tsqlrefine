using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers;

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
}
