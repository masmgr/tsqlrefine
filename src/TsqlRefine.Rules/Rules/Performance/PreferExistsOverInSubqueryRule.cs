using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects WHERE column IN (SELECT ...) patterns and recommends using EXISTS instead for better performance with large datasets.
/// </summary>
public sealed class PreferExistsOverInSubqueryRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "prefer-exists-over-in-subquery",
        Description: "Detects WHERE column IN (SELECT ...) patterns and recommends using EXISTS instead for better performance with large datasets.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new PreferExistsOverInSubqueryVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class PreferExistsOverInSubqueryVisitor : PredicateAwareVisitorBase
    {
        public override void ExplicitVisit(InPredicate node)
        {
            // Only flag IN with subquery, not IN with value lists
            if (IsInPredicate && node.Subquery is not null)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "Consider using EXISTS instead of IN with a subquery. EXISTS can be more efficient for large datasets as it short-circuits once a match is found.",
                    code: "prefer-exists-over-in-subquery",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
