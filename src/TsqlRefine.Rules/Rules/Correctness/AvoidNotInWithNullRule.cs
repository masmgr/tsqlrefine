using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects NOT IN with subquery which can produce unexpected empty results when the subquery returns NULL values.
/// </summary>
public sealed class AvoidNotInWithNullRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-not-in-with-null",
        Description: "Detects NOT IN with subquery which can produce unexpected empty results when the subquery returns NULL values.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidNotInWithNullVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidNotInWithNullVisitor : PredicateAwareVisitorBase
    {
        public override void ExplicitVisit(InPredicate node)
        {
            if (IsInPredicate && node.NotDefined && node.Subquery is not null)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "NOT IN with subquery can produce unexpected empty results when the subquery returns NULL values. Use NOT EXISTS or EXCEPT instead.",
                    code: "avoid-not-in-with-null",
                    category: "Correctness",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
