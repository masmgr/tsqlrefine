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
    /// Collects all named tables reachable from a table reference subtree.
    /// </summary>
    private static List<NamedTableRef> CollectNamedTables(TableReference tableRef)
    {
        var result = new List<NamedTableRef>();
        CollectNamedTablesCore(tableRef, result);
        return result;
    }

    private static void CollectNamedTablesCore(TableReference tableRef, List<NamedTableRef> result)
    {
        switch (tableRef)
        {
            case NamedTableReference named:
                var tableName = named.SchemaObject.BaseIdentifier?.Value;
                if (string.IsNullOrWhiteSpace(tableName))
                {
                    return;
                }

                result.Add(new NamedTableRef(
                    named.SchemaObject.SchemaIdentifier?.Value ?? "dbo",
                    tableName,
                    named.Alias?.Value ?? tableName));
                return;
            case JoinTableReference join:
                CollectNamedTablesCore(join.FirstTableReference, result);
                CollectNamedTablesCore(join.SecondTableReference, result);
                return;
            case JoinParenthesisTableReference paren when paren.Join is not null:
                CollectNamedTablesCore(paren.Join, result);
                return;
        }
    }

    /// <summary>
    /// Extracts column pairs from a JOIN ON clause search condition.
    /// Only extracts equality comparisons (<c>=</c>) between column references connected by AND.
    /// </summary>
    private static List<MatchedColumnPair> ExtractColumnPairs(
        BooleanExpression? condition,
        ISet<string> leftAliases,
        ISet<string> rightAliases)
    {
        var pairs = new List<MatchedColumnPair>();
        CollectEqualityPairs(condition, leftAliases, rightAliases, pairs);
        return pairs;
    }

    private static void CollectEqualityPairs(
        BooleanExpression? expression,
        ISet<string> leftAliases,
        ISet<string> rightAliases,
        List<MatchedColumnPair> pairs)
    {
        switch (expression)
        {
            case BooleanComparisonExpression { ComparisonType: BooleanComparisonType.Equals } comp:
                if (comp.FirstExpression is ColumnReferenceExpression leftCol &&
                    comp.SecondExpression is ColumnReferenceExpression rightCol)
                {
                    var pair = MatchColumnPair(leftCol, rightCol, leftAliases, rightAliases);
                    if (pair is not null)
                    {
                        pairs.Add(pair);
                    }
                }

                break;

            case BooleanBinaryExpression { BinaryExpressionType: BooleanBinaryExpressionType.And } binary:
                CollectEqualityPairs(binary.FirstExpression, leftAliases, rightAliases, pairs);
                CollectEqualityPairs(binary.SecondExpression, leftAliases, rightAliases, pairs);
                break;

            case BooleanParenthesisExpression paren:
                CollectEqualityPairs(paren.Expression, leftAliases, rightAliases, pairs);
                break;
        }
    }

    /// <summary>
    /// Matches a pair of column references to the left and right tables based on their table qualifiers.
    /// </summary>
    private static MatchedColumnPair? MatchColumnPair(
        ColumnReferenceExpression col1,
        ColumnReferenceExpression col2,
        ISet<string> leftAliases,
        ISet<string> rightAliases)
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
        if (IsInAliasSet(qualifier1, leftAliases) && IsInAliasSet(qualifier2, rightAliases))
        {
            return new MatchedColumnPair(qualifier1!, colName1, qualifier2!, colName2);
        }

        // col1 is right, col2 is left (swapped)
        if (IsInAliasSet(qualifier1, rightAliases) && IsInAliasSet(qualifier2, leftAliases))
        {
            return new MatchedColumnPair(qualifier2!, colName2, qualifier1!, colName1);
        }

        // If no qualifiers, use positional heuristic
        if (qualifier1 is null && qualifier2 is null &&
            TryGetSingleAlias(leftAliases, out var leftAlias) &&
            TryGetSingleAlias(rightAliases, out var rightAlias))
        {
            return new MatchedColumnPair(leftAlias, colName1, rightAlias, colName2);
        }

        return null;
    }

    private static bool IsInAliasSet(string? qualifier, ISet<string> aliases) =>
        qualifier is not null &&
        aliases.Contains(qualifier);

    private static bool TryGetSingleAlias(ISet<string> aliases, out string alias)
    {
        if (aliases.Count == 1)
        {
            alias = aliases.First();
            return true;
        }

        alias = string.Empty;
        return false;
    }

    private static JoinExtractionResult BuildJoinExtractionResult(
        IReadOnlyList<NamedTableRef> leftTables,
        IReadOnlyList<NamedTableRef> rightTables,
        IReadOnlyList<MatchedColumnPair> matchedPairs)
    {
        if (matchedPairs.Count == 0)
        {
            // Preserve previous fallback behavior when ON-clause columns cannot be mapped.
            return new JoinExtractionResult(
                leftTables[^1],
                rightTables[^1],
                []);
        }

        var bestAliasPair = matchedPairs
            .GroupBy(static p => (p.LeftAlias, p.RightAlias))
            .OrderByDescending(static g => g.Count())
            .ThenBy(static g => g.Key.LeftAlias, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static g => g.Key.RightAlias, StringComparer.OrdinalIgnoreCase)
            .First();

        var left = leftTables.FirstOrDefault(t =>
            string.Equals(t.AliasOrName, bestAliasPair.Key.LeftAlias, StringComparison.OrdinalIgnoreCase))
            ?? leftTables[^1];
        var right = rightTables.FirstOrDefault(t =>
            string.Equals(t.AliasOrName, bestAliasPair.Key.RightAlias, StringComparison.OrdinalIgnoreCase))
            ?? rightTables[^1];

        var pairs = bestAliasPair
            .Select(static p => new ColumnPair(p.LeftColumn, p.RightColumn))
            .ToList();

        return new JoinExtractionResult(left, right, pairs);
    }

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
            var leftTables = CollectNamedTables(node.FirstTableReference);
            var rightTables = CollectNamedTables(node.SecondTableReference);

            // Only process when both sides are named tables
            if (leftTables.Count > 0 && rightTables.Count > 0)
            {
                var leftAliases = leftTables
                    .Select(static t => t.AliasOrName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var rightAliases = rightTables
                    .Select(static t => t.AliasOrName)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var matchedPairs = ExtractColumnPairs(node.SearchCondition, leftAliases, rightAliases);
                var extraction = BuildJoinExtractionResult(leftTables, rightTables, matchedPairs);

                var joinType = JoinTypeNames.GetValueOrDefault(node.QualifiedJoinType, "INNER");

                Joins.Add(new RawJoinInfo(
                    extraction.LeftTable.Schema,
                    extraction.LeftTable.Table,
                    extraction.RightTable.Schema,
                    extraction.RightTable.Table,
                    joinType,
                    extraction.ColumnPairs,
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
                var leftTables = CollectNamedTables(node.FirstTableReference);
                var rightTables = CollectNamedTables(node.SecondTableReference);

                if (leftTables.Count > 0 && rightTables.Count > 0)
                {
                    var left = leftTables[^1];
                    var right = rightTables[^1];
                    Joins.Add(new RawJoinInfo(
                        left.Schema,
                        left.Table,
                        right.Schema,
                        right.Table,
                        "CROSS",
                        [],
                        _sourceFile
                    ));
                }
            }

            base.ExplicitVisit(node);
        }
    }

    private sealed record NamedTableRef(string Schema, string Table, string AliasOrName);

    private sealed record MatchedColumnPair(
        string LeftAlias,
        string LeftColumn,
        string RightAlias,
        string RightColumn);

    private sealed record JoinExtractionResult(
        NamedTableRef LeftTable,
        NamedTableRef RightTable,
        IReadOnlyList<ColumnPair> ColumnPairs);
}
