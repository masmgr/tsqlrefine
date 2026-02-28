using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Visitors;

/// <summary>
/// Base class for visitor rules that require both an <see cref="ISchemaProvider"/> and
/// an <see cref="IRelationDeviationProvider"/>.
/// When either is unavailable, the rule gracefully degrades by returning no diagnostics.
/// </summary>
public abstract class SchemaAndDeviationAwareVisitorRuleBase : DiagnosticVisitorRuleBase, IRule
{
    /// <summary>
    /// Analyzes the given context. Returns empty diagnostics if no schema or relation deviation data is available.
    /// </summary>
    public new IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Schema is null || context.RelationDeviations is null)
        {
            return [];
        }

        return base.Analyze(context);
    }

    /// <summary>
    /// Explicit interface re-implementation to ensure the null checks are applied
    /// even when the rule is invoked through the <see cref="IRule"/> interface.
    /// </summary>
    IEnumerable<Diagnostic> IRule.Analyze(RuleContext context) => Analyze(context);
}
