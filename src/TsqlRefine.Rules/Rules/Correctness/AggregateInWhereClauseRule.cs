using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects aggregate functions used directly in WHERE clauses.
/// SQL Server raises a runtime error for such usage.
/// </summary>
public sealed class AggregateInWhereClauseRule : DiagnosticVisitorRuleBase
{
    private const string RuleId = "aggregate-in-where-clause";
    private const string Category = "Correctness";

    public override RuleMetadata Metadata { get; } = new(
        RuleId: RuleId,
        Description: "Detects aggregate functions used directly in WHERE clauses.",
        Category: Category,
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AggregateInWhereClauseVisitor();

    private sealed class AggregateInWhereClauseVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(WhereClause node)
        {
            CheckBooleanExpressionForAggregates(node.SearchCondition);
            base.ExplicitVisit(node);
        }

        private void CheckBooleanExpressionForAggregates(BooleanExpression? boolExpr)
        {
            switch (boolExpr)
            {
                case null:
                    return;

                case BooleanComparisonExpression comparison:
                    CheckScalarExpressionForAggregate(comparison.FirstExpression);
                    CheckScalarExpressionForAggregate(comparison.SecondExpression);
                    return;

                case BooleanBinaryExpression binary:
                    CheckBooleanExpressionForAggregates(binary.FirstExpression);
                    CheckBooleanExpressionForAggregates(binary.SecondExpression);
                    return;

                case BooleanIsNullExpression isNull:
                    CheckScalarExpressionForAggregate(isNull.Expression);
                    return;

                case BooleanNotExpression not:
                    CheckBooleanExpressionForAggregates(not.Expression);
                    return;

                case BooleanParenthesisExpression paren:
                    CheckBooleanExpressionForAggregates(paren.Expression);
                    return;

                case InPredicate inPred:
                    CheckScalarExpressionForAggregate(inPred.Expression);
                    if (inPred.Subquery != null)
                    {
                        // IN (subquery) — don't descend into subquery scope
                        return;
                    }
                    if (inPred.Values != null)
                    {
                        foreach (var val in inPred.Values)
                        {
                            CheckScalarExpressionForAggregate(val);
                        }
                    }
                    return;

                case LikePredicate like:
                    CheckScalarExpressionForAggregate(like.FirstExpression);
                    CheckScalarExpressionForAggregate(like.SecondExpression);
                    return;

                case BooleanTernaryExpression ternary:
                    CheckScalarExpressionForAggregate(ternary.FirstExpression);
                    CheckScalarExpressionForAggregate(ternary.SecondExpression);
                    CheckScalarExpressionForAggregate(ternary.ThirdExpression);
                    return;

                case ExistsPredicate:
                    // Subquery scope — don't descend
                    return;
            }
        }

        private void CheckScalarExpressionForAggregate(ScalarExpression? expression)
        {
            switch (expression)
            {
                case null:
                    return;

                case FunctionCall func:
                    if (func.OverClause == null &&
                        func.FunctionName?.Value is { } name &&
                        AggregateFunctionHelpers.AggregateFunctions.Contains(name))
                    {
                        AddDiagnostic(
                            fragment: func,
                            message: $"Aggregate function '{name}' cannot be used directly in a WHERE clause. Use a HAVING clause or a subquery instead.",
                            code: RuleId,
                            category: Category,
                            fixable: false
                        );
                        return;
                    }
                    // Non-aggregate function — check parameters for nested aggregates
                    if (func.Parameters != null)
                    {
                        foreach (var param in func.Parameters)
                        {
                            CheckScalarExpressionForAggregate(param);
                        }
                    }
                    return;

                case BinaryExpression binary:
                    CheckScalarExpressionForAggregate(binary.FirstExpression);
                    CheckScalarExpressionForAggregate(binary.SecondExpression);
                    return;

                case ParenthesisExpression paren:
                    CheckScalarExpressionForAggregate(paren.Expression);
                    return;

                case CastCall cast:
                    CheckScalarExpressionForAggregate(cast.Parameter);
                    return;

                case ConvertCall convert:
                    CheckScalarExpressionForAggregate(convert.Parameter);
                    return;

                case SearchedCaseExpression searchedCase:
                    foreach (var when in searchedCase.WhenClauses)
                    {
                        CheckBooleanExpressionForAggregates(when.WhenExpression);
                        CheckScalarExpressionForAggregate(when.ThenExpression);
                    }
                    CheckScalarExpressionForAggregate(searchedCase.ElseExpression);
                    return;

                case SimpleCaseExpression simpleCase:
                    CheckScalarExpressionForAggregate(simpleCase.InputExpression);
                    foreach (var when in simpleCase.WhenClauses)
                    {
                        CheckScalarExpressionForAggregate(when.WhenExpression);
                        CheckScalarExpressionForAggregate(when.ThenExpression);
                    }
                    CheckScalarExpressionForAggregate(simpleCase.ElseExpression);
                    return;

                case CoalesceExpression coalesce:
                    foreach (var expr in coalesce.Expressions)
                    {
                        CheckScalarExpressionForAggregate(expr);
                    }
                    return;

                case NullIfExpression nullIf:
                    CheckScalarExpressionForAggregate(nullIf.FirstExpression);
                    CheckScalarExpressionForAggregate(nullIf.SecondExpression);
                    return;

                case IIfCall iif:
                    CheckBooleanExpressionForAggregates(iif.Predicate);
                    CheckScalarExpressionForAggregate(iif.ThenExpression);
                    CheckScalarExpressionForAggregate(iif.ElseExpression);
                    return;

                case UnaryExpression unary:
                    CheckScalarExpressionForAggregate(unary.Expression);
                    return;

                case ScalarSubquery:
                    // Don't descend into subqueries — they have their own scope
                    return;
            }

            // For column references, literals, and other leaf expressions — no aggregates
        }
    }
}
