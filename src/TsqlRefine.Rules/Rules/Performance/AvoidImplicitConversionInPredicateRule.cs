using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

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

    private sealed class AvoidImplicitConversionInPredicateVisitor : DiagnosticVisitorBase
    {
        private bool _isInPredicate = false;

        public override void ExplicitVisit(WhereClause node)
        {
            _isInPredicate = true;
            base.ExplicitVisit(node);
            _isInPredicate = false;
        }

        public override void ExplicitVisit(BooleanComparisonExpression node)
        {
            if (_isInPredicate)
            {
                CheckForFunctionOnColumn(node.FirstExpression);
                CheckForFunctionOnColumn(node.SecondExpression);
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(QualifiedJoin node)
        {
            // Check JOIN ON conditions
            if (node.SearchCondition != null)
            {
                _isInPredicate = true;
                node.SearchCondition.Accept(this);
                _isInPredicate = false;
            }

            // Continue visiting other parts of the join
            if (node.FirstTableReference != null)
                node.FirstTableReference.Accept(this);
            if (node.SecondTableReference != null)
                node.SecondTableReference.Accept(this);
        }

        public override void ExplicitVisit(HavingClause node)
        {
            _isInPredicate = true;
            base.ExplicitVisit(node);
            _isInPredicate = false;
        }

        private void CheckForFunctionOnColumn(ScalarExpression? expression)
        {
            if (expression == null)
                return;

            // Check for CAST/CONVERT only
            if (expression is CastCall castCall)
            {
                if (ContainsColumnReference(castCall.Parameter))
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
                if (ContainsColumnReference(convertCall.Parameter))
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

        private static bool ContainsColumnReference(ScalarExpression? expression)
        {
            if (expression == null)
                return false;

            // Direct column reference
            if (expression is ColumnReferenceExpression)
                return true;

            // Check nested expressions
            if (expression is CastCall castCall)
                return ContainsColumnReference(castCall.Parameter);

            if (expression is ConvertCall convertCall)
                return ContainsColumnReference(convertCall.Parameter);

            if (expression is FunctionCall functionCall && functionCall.Parameters != null)
            {
                for (var i = 0; i < functionCall.Parameters.Count; i++)
                {
                    var parameter = functionCall.Parameters[i];
                    if (DatePartHelper.IsDatePartLiteralParameter(functionCall, i, parameter))
                    {
                        continue;
                    }

                    if (ContainsColumnReference(parameter))
                    {
                        return true;
                    }
                }
                return false;
            }

            if (expression is BinaryExpression binaryExpr)
                return ContainsColumnReference(binaryExpr.FirstExpression) ||
                       ContainsColumnReference(binaryExpr.SecondExpression);

            if (expression is ParenthesisExpression parenExpr)
                return ContainsColumnReference(parenExpr.Expression);

            return false;
        }
    }
}
