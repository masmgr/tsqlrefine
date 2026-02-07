using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects UPPER or LOWER functions applied to columns in WHERE, JOIN ON, or HAVING predicates which prevents index usage
/// </summary>
public sealed class UpperLowerRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "upper-lower",
        Description: "Detects UPPER or LOWER functions applied to columns in WHERE, JOIN ON, or HAVING predicates which prevents index usage",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new UpperLowerVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class UpperLowerVisitor : PredicateAwareVisitorBase
    {
        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            if (IsInPredicate)
            {
                CheckForUpperLowerOnColumn(node.FirstExpression);
                CheckForUpperLowerOnColumn(node.SecondExpression);
            }

            base.ExplicitVisit(node);
        }

        private void CheckForUpperLowerOnColumn(ScalarExpression? expression)
        {
            if (expression == null)
                return;

            if (expression is FunctionCall functionCall)
            {
                var functionName = functionCall.FunctionName?.Value?.ToUpperInvariant();

                if (functionName is "UPPER" or "LOWER")
                {
                    // Check if function is applied to a column reference
                    if (functionCall.Parameters != null && functionCall.Parameters.Any(ExpressionAnalysisHelpers.ContainsColumnReference))
                    {
                        AddDiagnostic(
                            fragment: functionCall,
                            message: $"Avoid using {functionName}() on columns in predicates. This prevents index usage and causes performance issues. Consider using a case-insensitive collation or computed column with index instead.",
                            code: "upper-lower",
                            category: "Performance",
                            fixable: false
                        );
                    }
                }
            }
        }
    }
}
