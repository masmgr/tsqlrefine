using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects columns in HAVING clause that are neither in the GROUP BY clause nor wrapped in an aggregate function.
/// SQL Server raises error 8120 for such columns at runtime.
/// </summary>
public sealed class HavingColumnMismatchRule : DiagnosticVisitorRuleBase
{
    private const string RuleId = "having-column-mismatch";
    private const string Category = "Correctness";

    public override RuleMetadata Metadata { get; } = new(
        RuleId: RuleId,
        Description: "Detects columns in HAVING clause not in GROUP BY and not wrapped in an aggregate function.",
        Category: Category,
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new HavingColumnMismatchVisitor();

    private sealed class HavingColumnMismatchVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.HavingClause?.SearchCondition != null &&
                node.GroupByClause?.GroupingSpecifications is { Count: > 0 })
            {
                var groupByColumns = CollectGroupByColumns(node.GroupByClause);
                CheckHavingClause(node.HavingClause.SearchCondition, groupByColumns);
            }

            base.ExplicitVisit(node);
        }

        private static List<ColumnReferenceExpression> CollectGroupByColumns(GroupByClause groupBy)
        {
            var columns = new List<ColumnReferenceExpression>();

            foreach (var spec in groupBy.GroupingSpecifications)
            {
                if (spec is ExpressionGroupingSpecification exprSpec &&
                    exprSpec.Expression is ColumnReferenceExpression colRef)
                {
                    columns.Add(colRef);
                }
            }

            return columns;
        }

        private void CheckHavingClause(
            BooleanExpression searchCondition,
            List<ColumnReferenceExpression> groupByColumns)
        {
            var columnRefs = new List<ColumnReferenceExpression>();
            CollectColumnReferencesFromBooleanExpression(searchCondition, columnRefs);

            foreach (var colRef in columnRefs)
            {
                if (!IsInGroupBy(colRef, groupByColumns))
                {
                    var columnName = GetColumnDisplayName(colRef);
                    AddDiagnostic(
                        fragment: colRef,
                        message: $"Column '{columnName}' in HAVING clause is not contained in GROUP BY or an aggregate function.",
                        code: RuleId,
                        category: Category,
                        fixable: false
                    );
                }
            }
        }

        private static void CollectColumnReferencesFromBooleanExpression(
            BooleanExpression? boolExpr,
            List<ColumnReferenceExpression> result)
        {
            switch (boolExpr)
            {
                case null:
                    return;

                case BooleanComparisonExpression comparison:
                    CollectColumnReferences(comparison.FirstExpression, result);
                    CollectColumnReferences(comparison.SecondExpression, result);
                    return;

                case BooleanBinaryExpression binary:
                    CollectColumnReferencesFromBooleanExpression(binary.FirstExpression, result);
                    CollectColumnReferencesFromBooleanExpression(binary.SecondExpression, result);
                    return;

                case BooleanIsNullExpression isNull:
                    CollectColumnReferences(isNull.Expression, result);
                    return;

                case BooleanNotExpression not:
                    CollectColumnReferencesFromBooleanExpression(not.Expression, result);
                    return;

                case BooleanParenthesisExpression paren:
                    CollectColumnReferencesFromBooleanExpression(paren.Expression, result);
                    return;

                case InPredicate inPred:
                    CollectColumnReferences(inPred.Expression, result);
                    if (inPred.Values != null)
                    {
                        foreach (var val in inPred.Values)
                        {
                            CollectColumnReferences(val, result);
                        }
                    }
                    return;

                case LikePredicate like:
                    CollectColumnReferences(like.FirstExpression, result);
                    CollectColumnReferences(like.SecondExpression, result);
                    return;

                case BooleanTernaryExpression ternary:
                    CollectColumnReferences(ternary.FirstExpression, result);
                    CollectColumnReferences(ternary.SecondExpression, result);
                    CollectColumnReferences(ternary.ThirdExpression, result);
                    return;

                case ExistsPredicate:
                    // Subquery scope — don't descend
                    return;
            }
        }

        private static void CollectColumnReferences(
            ScalarExpression? expression,
            List<ColumnReferenceExpression> result)
        {
            switch (expression)
            {
                case null:
                    return;

                case ColumnReferenceExpression colRef:
                    result.Add(colRef);
                    return;

                case FunctionCall func:
                    if (AggregateFunctionHelpers.IsAggregateFunction(func))
                    {
                        // Don't collect column references inside aggregate functions
                        return;
                    }
                    if (func.Parameters != null)
                    {
                        foreach (var param in func.Parameters)
                        {
                            CollectColumnReferences(param, result);
                        }
                    }
                    return;

                case BinaryExpression binary:
                    CollectColumnReferences(binary.FirstExpression, result);
                    CollectColumnReferences(binary.SecondExpression, result);
                    return;

                case ParenthesisExpression paren:
                    CollectColumnReferences(paren.Expression, result);
                    return;

                case CastCall cast:
                    CollectColumnReferences(cast.Parameter, result);
                    return;

                case ConvertCall convert:
                    CollectColumnReferences(convert.Parameter, result);
                    return;

                case SearchedCaseExpression searchedCase:
                    foreach (var when in searchedCase.WhenClauses)
                    {
                        CollectColumnReferencesFromBooleanExpression(when.WhenExpression, result);
                        CollectColumnReferences(when.ThenExpression, result);
                    }
                    CollectColumnReferences(searchedCase.ElseExpression, result);
                    return;

                case SimpleCaseExpression simpleCase:
                    CollectColumnReferences(simpleCase.InputExpression, result);
                    foreach (var when in simpleCase.WhenClauses)
                    {
                        CollectColumnReferences(when.WhenExpression, result);
                        CollectColumnReferences(when.ThenExpression, result);
                    }
                    CollectColumnReferences(simpleCase.ElseExpression, result);
                    return;

                case CoalesceExpression coalesce:
                    foreach (var expr in coalesce.Expressions)
                    {
                        CollectColumnReferences(expr, result);
                    }
                    return;

                case NullIfExpression nullIf:
                    CollectColumnReferences(nullIf.FirstExpression, result);
                    CollectColumnReferences(nullIf.SecondExpression, result);
                    return;

                case IIfCall iif:
                    CollectColumnReferencesFromBooleanExpression(iif.Predicate, result);
                    CollectColumnReferences(iif.ThenExpression, result);
                    CollectColumnReferences(iif.ElseExpression, result);
                    return;

                case UnaryExpression unary:
                    CollectColumnReferences(unary.Expression, result);
                    return;

                case ScalarSubquery:
                    // Don't descend into subqueries — they have their own scope
                    return;
            }

            // For literals and other leaf expressions, no column references to collect
        }

        private static bool IsInGroupBy(
            ColumnReferenceExpression colRef,
            List<ColumnReferenceExpression> groupByColumns)
        {
            foreach (var gbCol in groupByColumns)
            {
                if (ColumnsMatch(colRef, gbCol))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ColumnsMatch(
            ColumnReferenceExpression a,
            ColumnReferenceExpression b)
        {
            var aIds = a.MultiPartIdentifier?.Identifiers;
            var bIds = b.MultiPartIdentifier?.Identifiers;

            if (aIds == null || bIds == null || aIds.Count == 0 || bIds.Count == 0)
            {
                return false;
            }

            // Compare column name (last identifier) — always required
            if (!string.Equals(aIds[^1].Value, bIds[^1].Value, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // If both have qualifiers, compare them
            if (aIds.Count > 1 && bIds.Count > 1)
            {
                return string.Equals(
                    aIds[aIds.Count - 2].Value,
                    bIds[bIds.Count - 2].Value,
                    StringComparison.OrdinalIgnoreCase);
            }

            // If one is qualified and the other is not, match on column name alone
            return true;
        }

        private static string GetColumnDisplayName(ColumnReferenceExpression colRef)
        {
            var ids = colRef.MultiPartIdentifier?.Identifiers;
            if (ids == null || ids.Count == 0)
            {
                return "?";
            }

            return string.Join(".", ids.Select(id => id.Value));
        }
    }
}
