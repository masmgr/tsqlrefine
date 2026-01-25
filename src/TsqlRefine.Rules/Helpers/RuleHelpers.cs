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
}
