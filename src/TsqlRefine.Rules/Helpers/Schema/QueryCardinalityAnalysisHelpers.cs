using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Schema;

/// <summary>Schema-backed proofs about the maximum number of rows returned by a query.</summary>
internal static class QueryCardinalityAnalysisHelpers
{
    /// <summary>
    /// Returns true when a simple single-table query filters on a complete primary or unique key.
    /// More complex query shapes are left unproven to avoid suppressing real diagnostics.
    /// </summary>
    internal static bool ReturnsAtMostOneRow(QuerySpecification query, ISchemaProvider? schema)
    {
        if (schema is null ||
            query.WhereClause?.SearchCondition is null ||
            query.FromClause?.TableReferences is not [NamedTableReference tableReference])
        {
            return false;
        }

        var aliasMap = AliasMapBuilder.Build([tableReference], schema);
        if (aliasMap.AllTables.Count != 1)
        {
            return false;
        }

        var equalityColumns = new List<(string? Qualifier, string ColumnName)>();
        CollectEqualityColumns(query.WhereClause.SearchCondition, equalityColumns);
        if (equalityColumns.Count == 0)
        {
            return false;
        }

        var resolvedTable = aliasMap.AllTables[0];
        var columns = equalityColumns
            .Where(column => column.Qualifier is null ||
                aliasMap.TryResolve(column.Qualifier, out var resolved) && resolved == resolvedTable)
            .Select(column => column.ColumnName)
            .ToArray();

        return columns.Length > 0 && schema.IsUniqueColumnSet(resolvedTable, columns);
    }

    private static void CollectEqualityColumns(
        BooleanExpression expression,
        List<(string? Qualifier, string ColumnName)> result)
    {
        switch (expression)
        {
            case BooleanComparisonExpression { ComparisonType: BooleanComparisonType.Equals } comparison:
                AddColumn(comparison.FirstExpression, result);
                AddColumn(comparison.SecondExpression, result);
                break;
            case BooleanBinaryExpression { BinaryExpressionType: BooleanBinaryExpressionType.And } binary:
                CollectEqualityColumns(binary.FirstExpression, result);
                CollectEqualityColumns(binary.SecondExpression, result);
                break;
            case BooleanParenthesisExpression parenthesis:
                CollectEqualityColumns(parenthesis.Expression, result);
                break;
        }
    }

    private static void AddColumn(
        ScalarExpression expression,
        List<(string? Qualifier, string ColumnName)> result)
    {
        if (expression is ColumnReferenceExpression column &&
            column.MultiPartIdentifier?.Identifiers is { Count: > 0 } identifiers)
        {
            result.Add((ColumnReferenceHelpers.GetTableQualifier(column), identifiers[^1].Value));
        }
    }
}
