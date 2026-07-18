using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects IN/EXISTS predicates that repeat an INNER JOIN to the same table and key.
/// </summary>
public sealed class RedundantSemiJoinRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "redundant-semi-join",
        Description: "Detects IN or EXISTS predicates that duplicate an existing INNER JOIN to the same table and key.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new RedundantSemiJoinVisitor(Metadata);

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RedundantSemiJoinVisitor(RuleMetadata metadata) : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(QuerySpecification node)
        {
            foreach (var match in RedundantSemiJoinAnalysisHelpers.FindMatches(node))
            {
                AddDiagnostic(
                    range: match.InPredicate is not null
                        ? ScriptDomHelpers.GetInKeywordRange(match.InPredicate)
                        : ScriptDomHelpers.GetFirstTokenRange(match.Predicate),
                    message: "This semi-join repeats an existing INNER JOIN to the same table and key; remove the redundant predicate and validate the execution plan.",
                    code: metadata.RuleId,
                    category: metadata.Category,
                    fixable: metadata.Fixable);
            }

            base.ExplicitVisit(node);
        }
    }
}
