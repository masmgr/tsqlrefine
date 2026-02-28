using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects UPDATE...FROM...JOIN statements where the joined table has a one-to-many relationship
/// with the target table, causing non-deterministic updates.
/// </summary>
public sealed class UpdateJoinCardinalityMismatchRule : SchemaAwareVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "update-join-cardinality-mismatch",
        Description: "Detects UPDATE...FROM...JOIN where the join may produce multiple rows per target row, causing non-deterministic updates.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new UpdateJoinCardinalityVisitor(context.Schema!);

    private sealed class UpdateJoinCardinalityVisitor(ISchemaProvider schema) : DiagnosticVisitorBase
    {
        private AliasMap? _currentAliasMap;
        private ResolvedTable? _currentTarget;
        private Dictionary<ColumnReferenceExpression, (ResolvedTable Table, string ColumnName)?> _resolvedColumnCache =
            new(ReferenceEqualityComparer.Instance);
        private Dictionary<(ResolvedTable Table, string ColumnName), bool> _columnExistsCache =
            new(ResolvedTableColumnKeyComparer.Instance);
        private Dictionary<string, (ResolvedTable Table, string ColumnName)?> _unqualifiedColumnResolutionCache =
            new(StringComparer.OrdinalIgnoreCase);

        public override void ExplicitVisit(UpdateStatement node)
        {
            var updateSpec = node.UpdateSpecification;
            if (updateSpec?.FromClause?.TableReferences is not { Count: > 0 } tableRefs)
            {
                base.ExplicitVisit(node);
                return;
            }

            var target = updateSpec.Target as NamedTableReference;
            if (target is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var tableName = target.SchemaObject.BaseIdentifier?.Value;
            if (tableName is null || tableName.StartsWith('#') || tableName.StartsWith('@'))
            {
                base.ExplicitVisit(node);
                return;
            }

            var aliasMap = AliasMapBuilder.Build(tableRefs, schema);
            var resolvedTarget = ResolveUpdateTarget(
                updateSpec, tableName,
                target.SchemaObject.DatabaseIdentifier?.Value,
                target.SchemaObject.SchemaIdentifier?.Value,
                aliasMap);

            if (resolvedTarget is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var previousMap = _currentAliasMap;
            var previousTarget = _currentTarget;
            var previousResolvedColumnCache = _resolvedColumnCache;
            var previousColumnExistsCache = _columnExistsCache;
            var previousUnqualifiedColumnResolutionCache = _unqualifiedColumnResolutionCache;

            _currentAliasMap = aliasMap;
            _currentTarget = resolvedTarget;
            _resolvedColumnCache = new Dictionary<ColumnReferenceExpression, (ResolvedTable Table, string ColumnName)?>(ReferenceEqualityComparer.Instance);
            _columnExistsCache = new Dictionary<(ResolvedTable Table, string ColumnName), bool>(ResolvedTableColumnKeyComparer.Instance);
            _unqualifiedColumnResolutionCache = new Dictionary<string, (ResolvedTable Table, string ColumnName)?>(StringComparer.OrdinalIgnoreCase);

            foreach (var tableRef in tableRefs)
            {
                TraverseJoins(tableRef);
            }

            _currentAliasMap = previousMap;
            _currentTarget = previousTarget;
            _resolvedColumnCache = previousResolvedColumnCache;
            _columnExistsCache = previousColumnExistsCache;
            _unqualifiedColumnResolutionCache = previousUnqualifiedColumnResolutionCache;

            base.ExplicitVisit(node);
        }

        private void TraverseJoins(TableReference tableRef)
        {
            switch (tableRef)
            {
                case QualifiedJoin qualifiedJoin:
                    if (qualifiedJoin.SearchCondition is not null)
                    {
                        CheckJoinCardinality(qualifiedJoin);
                    }

                    TraverseJoins(qualifiedJoin.FirstTableReference);
                    TraverseJoins(qualifiedJoin.SecondTableReference);
                    break;

                case JoinTableReference join:
                    TraverseJoins(join.FirstTableReference);
                    TraverseJoins(join.SecondTableReference);
                    break;

                case JoinParenthesisTableReference joinParen when joinParen.Join is not null:
                    TraverseJoins(joinParen.Join);
                    break;
            }
        }

        private void CheckJoinCardinality(QualifiedJoin joinNode)
        {
            var pairs = ExtractEqualityPairs(joinNode.SearchCondition);
            if (pairs.Count == 0)
            {
                return;
            }

            var targetColumns = new List<string>();
            var joinedColumns = new List<string>();
            ResolvedTable? joinedTable = null;

            foreach (var (leftCol, rightCol) in pairs)
            {
                var leftResolved = ResolveColumnToTable(leftCol);
                var rightResolved = ResolveColumnToTable(rightCol);
                if (leftResolved is null || rightResolved is null)
                {
                    continue;
                }

                var (leftTable, leftColName) = leftResolved.Value;
                var (rightTable, rightColName) = rightResolved.Value;

                if (TablesAreEqual(leftTable, _currentTarget!))
                {
                    if (joinedTable is not null && !TablesAreEqual(rightTable, joinedTable))
                    {
                        continue;
                    }

                    targetColumns.Add(leftColName);
                    joinedColumns.Add(rightColName);
                    joinedTable ??= rightTable;
                }
                else if (TablesAreEqual(rightTable, _currentTarget!))
                {
                    if (joinedTable is not null && !TablesAreEqual(leftTable, joinedTable))
                    {
                        continue;
                    }

                    targetColumns.Add(rightColName);
                    joinedColumns.Add(leftColName);
                    joinedTable ??= leftTable;
                }
            }

            if (targetColumns.Count == 0 || joinedTable is null)
            {
                return;
            }

            var cardinality = schema.EstimateJoinCardinality(
                _currentTarget!, targetColumns,
                joinedTable, joinedColumns);

            if (cardinality is JoinCardinality.OneToMany or JoinCardinality.ManyToMany)
            {
                AddDiagnostic(
                    fragment: joinNode.SearchCondition,
                    message: $"UPDATE may be non-deterministic: join to '{joinedTable.SchemaName}.{joinedTable.TableName}' can match multiple rows per '{_currentTarget!.SchemaName}.{_currentTarget.TableName}' row (join columns on '{joinedTable.TableName}' are not unique).",
                    code: "update-join-cardinality-mismatch",
                    category: "Schema",
                    fixable: false);
            }
        }

        private ResolvedTable? ResolveUpdateTarget(
            UpdateSpecification updateSpec,
            string tableName,
            string? dbName,
            string? schemaName,
            AliasMap aliasMap)
        {
            if (dbName is not null || schemaName is not null)
            {
                return schema.ResolveTable(dbName, schemaName, tableName);
            }

            if (aliasMap.TryResolve(tableName, out var mapped))
            {
                return mapped;
            }

            return schema.ResolveTable(null, null, tableName);
        }

        private (ResolvedTable Table, string ColumnName)? ResolveColumnToTable(ColumnReferenceExpression colRef)
        {
            if (_resolvedColumnCache.TryGetValue(colRef, out var cached))
            {
                return cached;
            }

            var resolved = ResolveColumnToTableCore(colRef);
            _resolvedColumnCache[colRef] = resolved;
            return resolved;
        }

        private (ResolvedTable Table, string ColumnName)? ResolveColumnToTableCore(ColumnReferenceExpression colRef)
        {
            if (_currentAliasMap is null || colRef.ColumnType == ColumnType.Wildcard)
            {
                return null;
            }

            var identifiers = colRef.MultiPartIdentifier?.Identifiers;
            if (identifiers is null or { Count: 0 })
            {
                return null;
            }

            var columnName = identifiers[identifiers.Count - 1].Value;

            if (identifiers.Count >= 2)
            {
                if (QualifierLookupKeyBuilder.TryResolve(_currentAliasMap, identifiers, out var resolved))
                {
                    return resolved is null ? null : (resolved, columnName);
                }

                return null;
            }

            if (_unqualifiedColumnResolutionCache.TryGetValue(columnName, out var unqualifiedCached))
            {
                return unqualifiedCached;
            }

            foreach (var table in _currentAliasMap.AllTables)
            {
                if (ColumnExists(table, columnName))
                {
                    var result = (table, columnName);
                    _unqualifiedColumnResolutionCache[columnName] = result;
                    return result;
                }
            }

            _unqualifiedColumnResolutionCache[columnName] = null;
            return null;
        }

        private bool ColumnExists(ResolvedTable table, string columnName)
        {
            if (_columnExistsCache.TryGetValue((table, columnName), out var exists))
            {
                return exists;
            }

            exists = schema.ResolveColumn(table, columnName) is not null;
            _columnExistsCache[(table, columnName)] = exists;
            return exists;
        }

        private static List<(ColumnReferenceExpression Left, ColumnReferenceExpression Right)> ExtractEqualityPairs(
            BooleanExpression condition)
        {
            var results = new List<(ColumnReferenceExpression, ColumnReferenceExpression)>();
            CollectEqualityPairs(condition, results);
            return results;
        }

        private static void CollectEqualityPairs(
            BooleanExpression condition,
            List<(ColumnReferenceExpression, ColumnReferenceExpression)> results)
        {
            switch (condition)
            {
                case BooleanComparisonExpression { ComparisonType: BooleanComparisonType.Equals } comparison
                    when comparison.FirstExpression is ColumnReferenceExpression leftCol
                      && comparison.SecondExpression is ColumnReferenceExpression rightCol:
                    results.Add((leftCol, rightCol));
                    break;

                case BooleanBinaryExpression binary:
                    CollectEqualityPairs(binary.FirstExpression, results);
                    CollectEqualityPairs(binary.SecondExpression, results);
                    break;

                case BooleanParenthesisExpression paren:
                    CollectEqualityPairs(paren.Expression, results);
                    break;
            }
        }

        private static bool TablesAreEqual(ResolvedTable a, ResolvedTable b) =>
            string.Equals(a.DatabaseName, b.DatabaseName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.SchemaName, b.SchemaName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.TableName, b.TableName, StringComparison.OrdinalIgnoreCase);

        private sealed class ResolvedTableColumnKeyComparer : IEqualityComparer<(ResolvedTable Table, string ColumnName)>
        {
            public static ResolvedTableColumnKeyComparer Instance { get; } = new();

            public bool Equals((ResolvedTable Table, string ColumnName) x, (ResolvedTable Table, string ColumnName) y) =>
                TablesAreEqual(x.Table, y.Table)
                && string.Equals(x.ColumnName, y.ColumnName, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode((ResolvedTable Table, string ColumnName) obj)
            {
                var hash = new HashCode();
                hash.Add(obj.Table.DatabaseName, StringComparer.OrdinalIgnoreCase);
                hash.Add(obj.Table.SchemaName, StringComparer.OrdinalIgnoreCase);
                hash.Add(obj.Table.TableName, StringComparer.OrdinalIgnoreCase);
                hash.Add(obj.ColumnName, StringComparer.OrdinalIgnoreCase);
                return hash.ToHashCode();
            }
        }
    }
}
