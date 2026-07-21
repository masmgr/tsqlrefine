using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Rules.Helpers.Analysis;

/// <summary>
/// Provides shared column and expression analysis for GROUP BY validation rules.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506", Justification = "Existing ScriptDOM analysis helper; tracked as coupling baseline debt.")]
public static class GroupByColumnAnalysisHelpers
{
    public static List<ScalarExpression> CollectGroupByExpressions(GroupByClause groupBy)
    {
        ArgumentNullException.ThrowIfNull(groupBy);
        var expressions = new List<ScalarExpression>();

        foreach (var specification in groupBy.GroupingSpecifications)
        {
            CollectGroupByExpressionsFromSpecification(specification, expressions);
        }

        return expressions;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1502", Justification = "Existing ScriptDOM expression dispatch; tracked as complexity baseline debt.")]
    public static void CollectColumnReferences(
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
            case ColumnReferenceExpression columnReference:
                if (columnReference.ColumnType == ColumnType.Wildcard)
                {
                    return;
                }
                result.Add(columnReference);
                return;
            case FunctionCall function:
                if (IsAggregateFunction(function))
                {
                    return;
                }
                foreach (var parameter in function.Parameters)
                {
                    CollectColumnReferences(parameter, result, groupByExpressions);
                }
                CollectColumnReferencesFromOverClause(function.OverClause, result, groupByExpressions);
                return;
            case BinaryExpression binary:
                CollectColumnReferences(binary.FirstExpression, result, groupByExpressions);
                CollectColumnReferences(binary.SecondExpression, result, groupByExpressions);
                return;
            case ParenthesisExpression parenthesis:
                CollectColumnReferences(parenthesis.Expression, result, groupByExpressions);
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
                foreach (var item in coalesce.Expressions)
                {
                    CollectColumnReferences(item, result, groupByExpressions);
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
                return;
        }
    }

    public static void CollectColumnReferencesFromBooleanExpression(
        BooleanExpression? booleanExpression,
        List<ColumnReferenceExpression> result,
        List<ScalarExpression> groupByExpressions)
    {
        switch (booleanExpression)
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
            case BooleanParenthesisExpression parenthesis:
                CollectColumnReferencesFromBooleanExpression(parenthesis.Expression, result, groupByExpressions);
                return;
            case InPredicate inPredicate:
                CollectColumnReferences(inPredicate.Expression, result, groupByExpressions);
                foreach (var value in inPredicate.Values)
                {
                    CollectColumnReferences(value, result, groupByExpressions);
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
                return;
        }
    }

    public static List<ColumnReferenceExpression> FilterUngroupedColumns(
        IEnumerable<ColumnReferenceExpression> columnReferences,
        List<ScalarExpression> groupByExpressions)
    {
        var result = new List<ColumnReferenceExpression>();
        var reportedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var columnReference in columnReferences)
        {
            if (!IsInGroupBy(columnReference, groupByExpressions) &&
                reportedColumns.Add(GetColumnDedupKey(columnReference)))
            {
                result.Add(columnReference);
            }
        }

        return result;
    }

    public static bool IsAggregateFunction(FunctionCall function) =>
        function.OverClause is null && AggregateFunctionHelpers.IsAggregateFunction(function);

    public static bool IsInGroupBy(
        ColumnReferenceExpression columnReference,
        List<ScalarExpression> groupByExpressions)
    {
        foreach (var groupByExpression in groupByExpressions)
        {
            if (groupByExpression is ColumnReferenceExpression groupByColumn &&
                ColumnsMatch(columnReference, groupByColumn))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsExpressionInGroupBy(
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

    public static bool AreEquivalentExpressions(ScalarExpression left, ScalarExpression right) =>
        ExpressionsMatch(left, right);

    public static string GetColumnDisplayName(ColumnReferenceExpression columnReference)
    {
        var identifiers = columnReference.MultiPartIdentifier?.Identifiers;
        return identifiers is null || identifiers.Count == 0
            ? "?"
            : string.Join(".", identifiers.Select(identifier => identifier.Value));
    }

    private static void CollectGroupByExpressionsFromSpecification(
        GroupingSpecification? specification,
        List<ScalarExpression> expressions)
    {
        switch (specification)
        {
            case null:
                return;
            case ExpressionGroupingSpecification expression when expression.Expression is not null:
                expressions.Add(expression.Expression);
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
                return;
        }
    }

    private static void CollectColumnReferencesFromOverClause(
        OverClause? overClause,
        List<ColumnReferenceExpression> result,
        List<ScalarExpression> groupByExpressions)
    {
        if (overClause is null)
        {
            return;
        }

        foreach (var partition in overClause.Partitions)
        {
            CollectColumnReferences(partition, result, groupByExpressions);
        }

        if (overClause.OrderByClause is not null)
        {
            foreach (var orderByElement in overClause.OrderByClause.OrderByElements)
            {
                CollectColumnReferences(orderByElement.Expression, result, groupByExpressions);
            }
        }
    }

    private static bool ColumnsMatch(
        ColumnReferenceExpression left,
        ColumnReferenceExpression right)
    {
        var leftIdentifiers = left.MultiPartIdentifier?.Identifiers;
        var rightIdentifiers = right.MultiPartIdentifier?.Identifiers;
        if (leftIdentifiers is null || rightIdentifiers is null ||
            leftIdentifiers.Count == 0 || rightIdentifiers.Count == 0)
        {
            return false;
        }

        if (!string.Equals(leftIdentifiers[^1].Value, rightIdentifiers[^1].Value, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (leftIdentifiers.Count > 1 && rightIdentifiers.Count > 1)
        {
            return string.Equals(
                leftIdentifiers[^2].Value,
                rightIdentifiers[^2].Value,
                StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static bool ExpressionsMatch(ScalarExpression left, ScalarExpression right)
    {
        left = UnwrapParenthesis(left);
        right = UnwrapParenthesis(right);
        if (left is ColumnReferenceExpression leftColumn && right is ColumnReferenceExpression rightColumn)
        {
            return ColumnsMatch(leftColumn, rightColumn);
        }

        var leftText = GetNormalizedExpressionText(left, unqualifyColumns: false);
        var rightText = GetNormalizedExpressionText(right, unqualifyColumns: false);
        if (leftText is not null && rightText is not null &&
            string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var leftColumns = CollectAllColumnReferences(left);
        var rightColumns = CollectAllColumnReferences(right);
        if (leftColumns.Count == 0 || leftColumns.Count != rightColumns.Count)
        {
            return false;
        }

        for (var index = 0; index < leftColumns.Count; index++)
        {
            if (!ColumnsMatch(leftColumns[index], rightColumns[index]))
            {
                return false;
            }
        }

        leftText = GetNormalizedExpressionText(left, unqualifyColumns: true);
        rightText = GetNormalizedExpressionText(right, unqualifyColumns: true);
        return leftText is not null && rightText is not null &&
            string.Equals(leftText, rightText, StringComparison.OrdinalIgnoreCase);
    }

    private static ScalarExpression UnwrapParenthesis(ScalarExpression expression)
    {
        while (expression is ParenthesisExpression parenthesis)
        {
            expression = parenthesis.Expression;
        }
        return expression;
    }

    private static string? GetNormalizedExpressionText(TSqlFragment fragment, bool unqualifyColumns)
    {
        var tokens = fragment.ScriptTokenStream;
        if (tokens is null || fragment.FirstTokenIndex < 0 || fragment.LastTokenIndex < fragment.FirstTokenIndex)
        {
            return null;
        }

        var columnsByStartToken = unqualifyColumns
            ? CollectAllColumnReferences(fragment).ToDictionary(column => column.FirstTokenIndex)
            : null;
        var builder = new StringBuilder();
        for (var index = fragment.FirstTokenIndex; index <= fragment.LastTokenIndex && index < tokens.Count; index++)
        {
            if (columnsByStartToken is not null &&
                columnsByStartToken.TryGetValue(index, out var column))
            {
                var identifiers = column.MultiPartIdentifier?.Identifiers;
                if (identifiers is not { Count: > 0 })
                {
                    return null;
                }

                builder.Append(identifiers[^1].Value);
                index = column.LastTokenIndex;
                continue;
            }

            if (tokens[index].TokenType is TSqlTokenType.WhiteSpace or
                TSqlTokenType.SingleLineComment or TSqlTokenType.MultilineComment)
            {
                continue;
            }
            builder.Append(NormalizeTokenText(tokens[index]));
        }
        return builder.ToString();
    }

    private static List<ColumnReferenceExpression> CollectAllColumnReferences(TSqlFragment fragment)
    {
        var visitor = new ColumnReferenceCollector();
        fragment.Accept(visitor);
        return visitor.Columns;
    }

    private sealed class ColumnReferenceCollector : TSqlFragmentVisitor
    {
        public List<ColumnReferenceExpression> Columns { get; } = [];

        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            if (node.ColumnType != ColumnType.Wildcard)
            {
                Columns.Add(node);
            }
        }
    }

    private static string NormalizeTokenText(TSqlParserToken token) =>
        token.TokenType == TSqlTokenType.QuotedIdentifier
            ? NormalizeQuotedIdentifier(token.Text)
            : token.Text;

    private static string NormalizeQuotedIdentifier(string text) =>
        text.Length >= 2 && text[0] == '[' && text[^1] == ']'
            ? text[1..^1].Replace("]]", "]", StringComparison.Ordinal)
            : text;

    private static string GetColumnDedupKey(ColumnReferenceExpression columnReference)
    {
        var identifiers = columnReference.MultiPartIdentifier?.Identifiers;
        return identifiers is { Count: > 0 }
            ? string.Join(".", identifiers.Select(identifier => identifier.Value))
            : $"{columnReference.FirstTokenIndex}:{columnReference.LastTokenIndex}";
    }
}
