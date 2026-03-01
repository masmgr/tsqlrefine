using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Visitors;

/// <summary>
/// Base class for schema-aware visitor rules that require an <see cref="ISchemaProvider"/>.
/// When no schema is available, the rule gracefully degrades by returning no diagnostics.
/// </summary>
public abstract class SchemaAwareVisitorRuleBase : DiagnosticVisitorRuleBase, IRule
{
    /// <summary>
    /// Analyzes the given context. Returns empty diagnostics if no schema is available.
    /// </summary>
    public new IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.SchemaContext is null)
        {
            return [];
        }

        return base.Analyze(context);
    }

    /// <summary>
    /// Explicit interface re-implementation to ensure the schema check is applied
    /// even when the rule is invoked through the <see cref="IRule"/> interface.
    /// </summary>
    IEnumerable<Diagnostic> IRule.Analyze(RuleContext context) => Analyze(context);
}
