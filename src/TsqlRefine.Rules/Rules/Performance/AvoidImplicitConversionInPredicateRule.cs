using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

public sealed class AvoidImplicitConversionInPredicateRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-implicit-conversion-in-predicate",
        Description: "Detects CAST or CONVERT applied to columns in predicates which can cause implicit type conversions and prevent index usage",
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

        var visitor = new AvoidImplicitConversionInPredicateVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidImplicitConversionInPredicateVisitor : PredicateAwareVisitorBase
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

            // Check for CAST/CONVERT only
            if (expression is CastCall castCall)
            {
                if (ExpressionAnalysisHelpers.ContainsColumnReference(castCall.Parameter))
                {
                    AddDiagnostic(
                        fragment: castCall,
                        message: "Avoid CAST on columns in predicates. This prevents index usage and can cause implicit type conversions. Consider storing data in the appropriate type or applying conversion to the literal value instead.",
                        code: "avoid-implicit-conversion-in-predicate",
                        category: "Performance",
                        fixable: false
                    );
                }
            }
            else if (expression is ConvertCall convertCall)
            {
                if (ExpressionAnalysisHelpers.ContainsColumnReference(convertCall.Parameter))
                {
                    AddDiagnostic(
                        fragment: convertCall,
                        message: "Avoid CONVERT on columns in predicates. This prevents index usage and can cause implicit type conversions. Consider storing data in the appropriate type or applying conversion to the literal value instead.",
                        code: "avoid-implicit-conversion-in-predicate",
                        category: "Performance",
                        fixable: false
                    );
                }
            }
        }
    }
}
