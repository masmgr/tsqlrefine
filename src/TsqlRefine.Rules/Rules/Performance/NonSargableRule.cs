using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects functions applied to columns in WHERE, JOIN ON, or HAVING predicates which prevents index usage (non-sargable predicates)
/// </summary>
public sealed class NonSargableRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "non-sargable",
        Description: "Detects functions applied to columns in WHERE, JOIN ON, or HAVING predicates which prevents index usage (non-sargable predicates)",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new NonSargableVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class NonSargableVisitor : PredicateAwareVisitorBase
    {
        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            if (IsInPredicate)
            {
                CheckForFunctionOnColumn(node.FirstExpression);
                CheckForFunctionOnColumn(node.SecondExpression);
            }

            base.ExplicitVisit(node);
        }

        private void CheckForFunctionOnColumn(ScalarExpression? expression)
        {
            if (expression == null)
                return;

            // Check for functions on columns (excluding CAST/CONVERT which are handled by avoid-implicit-conversion-in-predicate)
            if (expression is FunctionCall functionCall)
            {
                var functionName = functionCall.FunctionName?.Value?.ToUpperInvariant();

                // CAST/CONVERT are covered by avoid-implicit-conversion-in-predicate rule
                if (functionName is "CAST" or "CONVERT")
                {
                    return;
                }

                // Check if function is applied to a column reference
                if (functionCall.Parameters != null && functionCall.Parameters.Any(ExpressionAnalysisHelpers.ContainsColumnReference))
                {
                    AddDiagnostic(
                        fragment: functionCall,
                        message: $"Avoid applying function '{functionName ?? "function"}' to columns in predicates. This prevents index usage (non-sargable) and causes performance issues. Consider using computed columns with indexes or rewriting the query.",
                        code: "non-sargable",
                        category: "Performance",
                        fixable: false
                    );
                }
            }
        }
    }
}
