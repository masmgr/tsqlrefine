using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers;

/// <summary>
/// Base class for TSqlFragmentVisitor implementations that collect diagnostics.
/// Provides standard pattern for diagnostic collection and Range conversion.
/// </summary>
public abstract class DiagnosticVisitorBase : TSqlFragmentVisitor
{
    private readonly List<Diagnostic> _diagnostics = new();

    /// <summary>
    /// Gets the collected diagnostics.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

    /// <summary>
    /// Adds a diagnostic to the collection.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when diagnostic is null.</exception>
    protected void AddDiagnostic(Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);
        _diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// Convenience method to create and add a diagnostic with a Range from a TSqlFragment.
    /// </summary>
    /// <param name="fragment">The TSqlFragment to get the range from.</param>
    /// <param name="message">The diagnostic message.</param>
    /// <param name="code">The diagnostic code (rule ID).</param>
    /// <param name="category">The diagnostic category.</param>
    /// <param name="fixable">Whether the diagnostic is fixable.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    protected void AddDiagnostic(
        TSqlFragment fragment,
        string message,
        string code,
        string category,
        bool fixable)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(category);

        _diagnostics.Add(new Diagnostic(
            Range: ScriptDomHelpers.GetRange(fragment),
            Message: message,
            Code: code,
            Data: new DiagnosticData(code, category, fixable)
        ));
    }
}
