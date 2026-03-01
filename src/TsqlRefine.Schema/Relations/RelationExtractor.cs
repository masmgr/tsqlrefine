using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TsqlRefine.Schema.Relations;

/// <summary>
/// Extracts JOIN patterns from a parsed SQL AST.
/// </summary>
internal static class RelationExtractor
{
    private static readonly FrozenDictionary<QualifiedJoinType, string> JoinTypeNames =
        new Dictionary<QualifiedJoinType, string>
        {
            [QualifiedJoinType.Inner] = "INNER",
            [QualifiedJoinType.LeftOuter] = "LEFT",
            [QualifiedJoinType.RightOuter] = "RIGHT",
            [QualifiedJoinType.FullOuter] = "FULL",
        }.ToFrozenDictionary();

    /// <summary>
    /// Extracts all JOIN relationships from a parsed SQL AST.
    /// </summary>
    internal static List<RawJoinInfo> Extract(TSqlFragment fragment, string sourceFile)
    {
        var visitor = new JoinCollectorVisitor(sourceFile);
        fragment.Accept(visitor);
        return visitor.Joins;
    }

    /// <summary>
    /// Resolves the table name and schema from a table reference.
    /// For nested JOINs (e.g., <c>A JOIN B JOIN C</c>), returns the rightmost named table
    /// since that is the most recently joined table participating in the outer ON clause.
    /// Returns null if the reference cannot be resolved to a named table.
    /// </summary>
    private static (string Schema, string Table, string AliasOrName)? ResolveNamedTable(
        TableReference tableRef) =>
        tableRef switch
        {
            NamedTableReference named => (
                named.SchemaObject.SchemaIdentifier?.Value ?? "dbo",
                named.SchemaObject.BaseIdentifier.Value,
                named.Alias?.Value ?? named.SchemaObject.BaseIdentifier.Value),
            QualifiedJoin join => ResolveNamedTable(join.SecondTableReference),
            UnqualifiedJoin join => ResolveNamedTable(join.SecondTableReference),
            JoinParenthesisTableReference paren => ResolveNamedTable(paren.Join),
            _ => null
        };

    /// <summary>
    /// Extracts column pairs from a JOIN ON clause search condition.
    /// Only extracts equality comparisons (<c>=</c>) between column references connected by AND.
    /// </summary>
    private static List<ColumnPair> ExtractColumnPairs(
        BooleanExpression? condition,
        string leftAliasOrName,
        string rightAliasOrName)
    {
        var pairs = new List<ColumnPair>();
        CollectEqualityPairs(condition, leftAliasOrName, rightAliasOrName, pairs);
        return pairs;
    }

    private static void CollectEqualityPairs(
        BooleanExpression? expression,
        string leftAlias,
        string rightAlias,
        List<ColumnPair> pairs)
    {
        switch (expression)
        {
            case BooleanComparisonExpression { ComparisonType: BooleanComparisonType.Equals } comp:
                if (comp.FirstExpression is ColumnReferenceExpression leftCol &&
                    comp.SecondExpression is ColumnReferenceExpression rightCol)
                {
                    var pair = MatchColumnPair(leftCol, rightCol, leftAlias, rightAlias);
                    if (pair is not null)
                    {
                        pairs.Add(pair);
                    }
                }

                break;

            case BooleanBinaryExpression { BinaryExpressionType: BooleanBinaryExpressionType.And } binary:
                CollectEqualityPairs(binary.FirstExpression, leftAlias, rightAlias, pairs);
                CollectEqualityPairs(binary.SecondExpression, leftAlias, rightAlias, pairs);
                break;

            case BooleanParenthesisExpression paren:
                CollectEqualityPairs(paren.Expression, leftAlias, rightAlias, pairs);
                break;
        }
    }

    /// <summary>
    /// Matches a pair of column references to the left and right tables based on their table qualifiers.
    /// </summary>
    private static ColumnPair? MatchColumnPair(
        ColumnReferenceExpression col1,
        ColumnReferenceExpression col2,
        string leftAlias,
        string rightAlias)
    {
        var qualifier1 = GetTableQualifier(col1);
        var qualifier2 = GetTableQualifier(col2);
        var colName1 = GetColumnName(col1);
        var colName2 = GetColumnName(col2);

        if (colName1 is null || colName2 is null)
        {
            return null;
        }

        // col1 is left, col2 is right
        if (IsMatch(qualifier1, leftAlias) && IsMatch(qualifier2, rightAlias))
        {
            return new ColumnPair(colName1, colName2);
        }

        // col1 is right, col2 is left (swapped)
        if (IsMatch(qualifier1, rightAlias) && IsMatch(qualifier2, leftAlias))
        {
            return new ColumnPair(colName2, colName1);
        }

        // If no qualifiers, use positional heuristic
        if (qualifier1 is null && qualifier2 is null)
        {
            return new ColumnPair(colName1, colName2);
        }

        return null;
    }

    private static bool IsMatch(string? qualifier, string aliasOrName) =>
        qualifier is not null &&
        string.Equals(qualifier, aliasOrName, StringComparison.OrdinalIgnoreCase);

    private static string? GetTableQualifier(ColumnReferenceExpression colRef)
    {
        var ids = colRef.MultiPartIdentifier?.Identifiers;
        if (ids is null || ids.Count < 2)
        {
            return null;
        }

        return ids[ids.Count - 2].Value;
    }

    private static string? GetColumnName(ColumnReferenceExpression colRef)
    {
        var ids = colRef.MultiPartIdentifier?.Identifiers;
        if (ids is null || ids.Count == 0)
        {
            return null;
        }

        return ids[ids.Count - 1].Value;
    }

    private sealed class JoinCollectorVisitor : TSqlFragmentVisitor
    {
        private readonly string _sourceFile;

        internal JoinCollectorVisitor(string sourceFile)
        {
            _sourceFile = sourceFile;
        }

        internal List<RawJoinInfo> Joins { get; } = [];

        public override void ExplicitVisit(QualifiedJoin node)
        {
            // Resolve left and right table references
            var left = ResolveNamedTable(node.FirstTableReference);
            var right = ResolveNamedTable(node.SecondTableReference);

            // Only process when both sides are named tables
            if (left is not null && right is not null)
            {
                var joinType = JoinTypeNames.GetValueOrDefault(node.QualifiedJoinType, "INNER");
                var columnPairs = ExtractColumnPairs(
                    node.SearchCondition, left.Value.AliasOrName, right.Value.AliasOrName);

                Joins.Add(new RawJoinInfo(
                    left.Value.Schema,
                    left.Value.Table,
                    right.Value.Schema,
                    right.Value.Table,
                    joinType,
                    columnPairs,
                    _sourceFile
                ));
            }

            // Continue visiting child nodes (handles nested JOINs)
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(UnqualifiedJoin node)
        {
            // Handle CROSS JOIN (unqualified)
            if (node.UnqualifiedJoinType == UnqualifiedJoinType.CrossJoin)
            {
                var left = ResolveNamedTable(node.FirstTableReference);
                var right = ResolveNamedTable(node.SecondTableReference);

                if (left is not null && right is not null)
                {
                    Joins.Add(new RawJoinInfo(
                        left.Value.Schema,
                        left.Value.Table,
                        right.Value.Schema,
                        right.Value.Table,
                        "CROSS",
                        [],
                        _sourceFile
                    ));
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
