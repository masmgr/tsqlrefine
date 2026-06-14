using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Visitors;

/// <summary>
/// Base class for deviation-aware visitor rules that require an <see cref="IRelationDeviationProvider"/>.
/// When no relation deviation data is available, the rule gracefully degrades by returning no diagnostics.
/// </summary>
public abstract class DeviationAwareVisitorRuleBase : DiagnosticVisitorRuleBase, IRule
{
    /// <summary>
    /// Analyzes the given context. Returns empty diagnostics if no relation deviation data is available.
    /// </summary>
    public new IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.RelationDeviations is null)
        {
            return [];
        }

        return base.Analyze(context);
    }

    /// <summary>
    /// Explicit interface re-implementation to ensure the deviation check is applied
    /// even when the rule is invoked through the <see cref="IRule"/> interface.
    /// </summary>
    IEnumerable<Diagnostic> IRule.Analyze(RuleContext context) => Analyze(context);
}
