using System.Text;
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
                var groupByExpressions = CollectGroupByExpressions(node.GroupByClause);
                CheckHavingClause(node.HavingClause.SearchCondition, groupByExpressions);
            }

            base.ExplicitVisit(node);
        }

        private static List<ScalarExpression> CollectGroupByExpressions(GroupByClause groupBy)
        {
            var expressions = new List<ScalarExpression>();

            foreach (var spec in groupBy.GroupingSpecifications)
            {
                CollectGroupByExpressionsFromSpecification(spec, expressions);
            }

            return expressions;
        }

        private static void CollectGroupByExpressionsFromSpecification(
            GroupingSpecification? specification,
            List<ScalarExpression> expressions)
        {
            switch (specification)
            {
                case null:
                    return;

                case ExpressionGroupingSpecification exprSpec when exprSpec.Expression is not null:
                    expressions.Add(exprSpec.Expression);
                    return;

                case CompositeGroupingSpecification composite:
                    foreach (var item in composite.Items)
                    {
                        CollectGroupByExpressionsFromSpecification(item, expressions);
                    }
                    return;

                case CubeGroupingSpecification cube:
                    foreach (var argument in cube.Arguments)
                    {
                        CollectGroupByExpressionsFromSpecification(argument, expressions);
                    }
                    return;

                case RollupGroupingSpecification rollup:
                    foreach (var argument in rollup.Arguments)
                    {
                        CollectGroupByExpressionsFromSpecification(argument, expressions);
                    }
                    return;

                case GroupingSetsGroupingSpecification groupingSets:
                    foreach (var groupingSet in groupingSets.Sets)
                    {
                        CollectGroupByExpressionsFromSpecification(groupingSet, expressions);
                    }
                    return;

                case GrandTotalGroupingSpecification:
                    // GROUPING SETS(()) has no expressions to collect.
                    return;
            }
        }

        private void CheckHavingClause(
            BooleanExpression searchCondition,
            List<ScalarExpression> groupByExpressions)
        {
            var columnRefs = new List<ColumnReferenceExpression>();
            CollectColumnReferencesFromBooleanExpression(searchCondition, columnRefs, groupByExpressions);
            var reportedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var colRef in columnRefs)
            {
                if (!IsInGroupBy(colRef, groupByExpressions))
                {
                    var dedupKey = GetColumnDedupKey(colRef);
                    if (!reportedColumns.Add(dedupKey))
                    {
                        continue;
                    }

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
            List<ColumnReferenceExpression> result,
            List<ScalarExpression> groupByExpressions)
        {
            switch (boolExpr)
            {
                case null:
                    return;

                case BooleanComparisonExpression comparison:
                    CollectColumnReferences(comparison.FirstExpression, result, groupByExpressions);
                    CollectColumnReferences(comparison.SecondExpression, result, groupByExpressions);
                    return;

                case BooleanBinaryExpression binary:
                    CollectColumnReferencesFromBooleanExpression(binary.FirstExpression, result, groupByExpressions);
                    CollectColumnReferencesFromBooleanExpression(binary.SecondExpression, result, groupByExpressions);
                    return;

                case BooleanIsNullExpression isNull:
                    CollectColumnReferences(isNull.Expression, result, groupByExpressions);
                    return;

                case BooleanNotExpression not:
                    CollectColumnReferencesFromBooleanExpression(not.Expression, result, groupByExpressions);
                    return;

                case BooleanParenthesisExpression paren:
                    CollectColumnReferencesFromBooleanExpression(paren.Expression, result, groupByExpressions);
                    return;

                case InPredicate inPred:
                    CollectColumnReferences(inPred.Expression, result, groupByExpressions);
                    if (inPred.Values != null)
                    {
                        foreach (var val in inPred.Values)
                        {
                            CollectColumnReferences(val, result, groupByExpressions);
                        }
                    }
                    return;

                case LikePredicate like:
                    CollectColumnReferences(like.FirstExpression, result, groupByExpressions);
                    CollectColumnReferences(like.SecondExpression, result, groupByExpressions);
                    return;

                case BooleanTernaryExpression ternary:
                    CollectColumnReferences(ternary.FirstExpression, result, groupByExpressions);
                    CollectColumnReferences(ternary.SecondExpression, result, groupByExpressions);
                    CollectColumnReferences(ternary.ThirdExpression, result, groupByExpressions);
                    return;

                case ExistsPredicate:
                    // Subquery scope — don't descend
                    return;
            }
        }

        private static void CollectColumnReferences(
            ScalarExpression? expression,
            List<ColumnReferenceExpression> result,
            List<ScalarExpression> groupByExpressions)
        {
            if (expression is not null && IsExpressionInGroupBy(expression, groupByExpressions))
            {
                return;
            }

            switch (expression)
            {
                case null:
                    return;

                case ColumnReferenceExpression colRef:
                    result.Add(colRef);
                    return;

                case FunctionCall func:
                    if (IsAggregateOrWindowFunction(func))
                    {
                        // Window/aggregate function internals have independent semantics for this rule.
                        return;
                    }
                    if (func.Parameters != null)
                    {
                        foreach (var param in func.Parameters)
                        {
                            CollectColumnReferences(param, result, groupByExpressions);
                        }
                    }
                    return;

                case BinaryExpression binary:
                    CollectColumnReferences(binary.FirstExpression, result, groupByExpressions);
                    CollectColumnReferences(binary.SecondExpression, result, groupByExpressions);
                    return;

                case ParenthesisExpression paren:
                    CollectColumnReferences(paren.Expression, result, groupByExpressions);
                    return;

                case CastCall cast:
                    CollectColumnReferences(cast.Parameter, result, groupByExpressions);
                    return;

                case ConvertCall convert:
                    CollectColumnReferences(convert.Parameter, result, groupByExpressions);
                    return;

                case SearchedCaseExpression searchedCase:
                    foreach (var when in searchedCase.WhenClauses)
                    {
                        CollectColumnReferencesFromBooleanExpression(when.WhenExpression, result, groupByExpressions);
                        CollectColumnReferences(when.ThenExpression, result, groupByExpressions);
                    }
                    CollectColumnReferences(searchedCase.ElseExpression, result, groupByExpressions);
                    return;

                case SimpleCaseExpression simpleCase:
                    CollectColumnReferences(simpleCase.InputExpression, result, groupByExpressions);
                    foreach (var when in simpleCase.WhenClauses)
                    {
                        CollectColumnReferences(when.WhenExpression, result, groupByExpressions);
                        CollectColumnReferences(when.ThenExpression, result, groupByExpressions);
                    }
                    CollectColumnReferences(simpleCase.ElseExpression, result, groupByExpressions);
                    return;

                case CoalesceExpression coalesce:
                    foreach (var expr in coalesce.Expressions)
                    {
                        CollectColumnReferences(expr, result, groupByExpressions);
                    }
                    return;

                case NullIfExpression nullIf:
                    CollectColumnReferences(nullIf.FirstExpression, result, groupByExpressions);
                    CollectColumnReferences(nullIf.SecondExpression, result, groupByExpressions);
                    return;

                case IIfCall iif:
                    CollectColumnReferencesFromBooleanExpression(iif.Predicate, result, groupByExpressions);
                    CollectColumnReferences(iif.ThenExpression, result, groupByExpressions);
                    CollectColumnReferences(iif.ElseExpression, result, groupByExpressions);
                    return;

                case UnaryExpression unary:
                    CollectColumnReferences(unary.Expression, result, groupByExpressions);
                    return;

                case ScalarSubquery:
                    // Don't descend into subqueries — they have their own scope
                    return;
            }

            // For literals and other leaf expressions, no column references to collect
        }

        private static bool IsInGroupBy(
            ColumnReferenceExpression colRef,
            List<ScalarExpression> groupByExpressions)
        {
            foreach (var groupByExpression in groupByExpressions)
            {
                if (groupByExpression is ColumnReferenceExpression gbCol &&
                    ColumnsMatch(colRef, gbCol))
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

        private static bool IsExpressionInGroupBy(
            ScalarExpression expression,
            List<ScalarExpression> groupByExpressions)
        {
            foreach (var groupByExpression in groupByExpressions)
            {
                if (ExpressionsMatch(expression, groupByExpression))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ExpressionsMatch(ScalarExpression left, ScalarExpression right)
        {
            left = UnwrapParenthesis(left);
            right = UnwrapParenthesis(right);

            if (left is ColumnReferenceExpression leftColumn &&
                right is ColumnReferenceExpression rightColumn)
            {
                return ColumnsMatch(leftColumn, rightColumn);
            }

            var leftText = GetNormalizedExpressionText(left);
            var rightText = GetNormalizedExpressionText(right);
            if (leftText is null || rightText is null)
            {
                return false;
            }

            return string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase);
        }

        private static ScalarExpression UnwrapParenthesis(ScalarExpression expression)
        {
            while (expression is ParenthesisExpression paren)
            {
                expression = paren.Expression;
            }

            return expression;
        }

        private static string? GetNormalizedExpressionText(TSqlFragment fragment)
        {
            var tokens = fragment.ScriptTokenStream;
            if (tokens is null || fragment.FirstTokenIndex < 0 || fragment.LastTokenIndex < fragment.FirstTokenIndex)
            {
                return null;
            }

            var sb = new StringBuilder();
            for (var i = fragment.FirstTokenIndex; i <= fragment.LastTokenIndex && i < tokens.Count; i++)
            {
                var tokenType = tokens[i].TokenType;
                if (tokenType is TSqlTokenType.WhiteSpace or TSqlTokenType.SingleLineComment or TSqlTokenType.MultilineComment)
                {
                    continue;
                }

                sb.Append(NormalizeTokenText(tokens[i]));
            }

            return sb.ToString();
        }

        private static bool IsAggregateOrWindowFunction(FunctionCall func) =>
            func.OverClause != null || AggregateFunctionHelpers.IsAggregateFunction(func);

        private static string NormalizeTokenText(TSqlParserToken token)
        {
            if (token.TokenType == TSqlTokenType.QuotedIdentifier)
            {
                return NormalizeQuotedIdentifier(token.Text);
            }

            return token.Text;
        }

        private static string NormalizeQuotedIdentifier(string text)
        {
            if (text.Length >= 2 && text[0] == '[' && text[^1] == ']')
            {
                return text[1..^1].Replace("]]", "]", StringComparison.Ordinal);
            }

            return text;
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

        private static string GetColumnDedupKey(ColumnReferenceExpression colRef)
        {
            var ids = colRef.MultiPartIdentifier?.Identifiers;
            if (ids is { Count: > 0 })
            {
                return string.Join(".", ids.Select(id => id.Value));
            }

            return $"{colRef.FirstTokenIndex}:{colRef.LastTokenIndex}";
        }
    }
}
