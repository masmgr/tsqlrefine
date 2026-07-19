using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects STRING_AGG without WITHIN GROUP (ORDER BY), which may produce non-deterministic string concatenation results.
/// </summary>
public sealed class StringAggWithoutOrderByRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "string-agg-without-order-by",
        Description: "Detects STRING_AGG without WITHIN GROUP (ORDER BY), which may produce non-deterministic string concatenation results.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override bool ShouldAnalyze(RuleContext context) => context.CompatLevel >= 140;

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) => new StringAggWithoutOrderByVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class StringAggWithoutOrderByVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(FunctionCall node)
        {
            // Check for STRING_AGG function
            if (node.FunctionName.Value.Equals("STRING_AGG", StringComparison.OrdinalIgnoreCase))
            {
                // Check if WITHIN GROUP (ORDER BY ...) clause is missing
                if (node.WithinGroupClause is null)
                {
                    AddDiagnostic(
                        fragment: node,
                        message: "STRING_AGG lacks WITHIN GROUP (ORDER BY ...); results may be non-deterministic.",
                        code: "string-agg-without-order-by",
                        category: "Correctness",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
