using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects JOINs where the ON columns match a foreign key relationship but the joined table
/// differs from the FK target, indicating a likely accidental join to the wrong table.
/// </summary>
public sealed class JoinForeignKeyMismatchRule : SchemaAwareVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "join-foreign-key-mismatch",
        Description: "Detects JOINs where the ON columns match a foreign key relationship but the joined table differs from the FK target.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new JoinForeignKeyMismatchVisitor(context.Schema!);

    private sealed class JoinForeignKeyMismatchVisitor(ISchemaProvider schema) : DiagnosticVisitorBase
    {
        private AliasMap? _currentAliasMap;
        private Dictionary<ColumnReferenceExpression, (ResolvedTable Table, string ColumnName)?> _resolvedColumnCache =
            new(ReferenceEqualityComparer.Instance);
        private Dictionary<(ResolvedTable Table, string ColumnName), bool> _columnExistsCache =
            new(ResolvedTableColumnKeyComparer.Instance);
        private Dictionary<string, (ResolvedTable Table, string ColumnName)?> _unqualifiedColumnResolutionCache =
            new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<ResolvedTable, IReadOnlyList<SchemaForeignKeyInfo>> _foreignKeysCache =
            new(ResolvedTableKeyComparer.Instance);
        private Dictionary<ResolvedTable, IReadOnlyList<SchemaForeignKeyInfo>> _referencingForeignKeysCache =
            new(ResolvedTableKeyComparer.Instance);

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.FromClause?.TableReferences is { Count: > 0 } tableRefs)
            {
                var previousMap = _currentAliasMap;
                var previousResolvedColumnCache = _resolvedColumnCache;
                var previousColumnExistsCache = _columnExistsCache;
                var previousUnqualifiedColumnResolutionCache = _unqualifiedColumnResolutionCache;
                var previousForeignKeysCache = _foreignKeysCache;
                var previousReferencingForeignKeysCache = _referencingForeignKeysCache;

                _currentAliasMap = AliasMapBuilder.Build(tableRefs, schema);
                _resolvedColumnCache = new Dictionary<ColumnReferenceExpression, (ResolvedTable Table, string ColumnName)?>(ReferenceEqualityComparer.Instance);
                _columnExistsCache = new Dictionary<(ResolvedTable Table, string ColumnName), bool>(ResolvedTableColumnKeyComparer.Instance);
                _unqualifiedColumnResolutionCache = new Dictionary<string, (ResolvedTable Table, string ColumnName)?>(StringComparer.OrdinalIgnoreCase);
                _foreignKeysCache = new Dictionary<ResolvedTable, IReadOnlyList<SchemaForeignKeyInfo>>(ResolvedTableKeyComparer.Instance);
                _referencingForeignKeysCache = new Dictionary<ResolvedTable, IReadOnlyList<SchemaForeignKeyInfo>>(ResolvedTableKeyComparer.Instance);

                node.FromClause.Accept(this);

                _currentAliasMap = previousMap;
                _resolvedColumnCache = previousResolvedColumnCache;
                _columnExistsCache = previousColumnExistsCache;
                _unqualifiedColumnResolutionCache = previousUnqualifiedColumnResolutionCache;
                _foreignKeysCache = previousForeignKeysCache;
                _referencingForeignKeysCache = previousReferencingForeignKeysCache;
                return;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(QualifiedJoin node)
        {
            if (_currentAliasMap is not null && node.SearchCondition is not null)
            {
                CheckJoinForeignKeyMismatch(node.SearchCondition);
            }

            base.ExplicitVisit(node);
        }

        private void CheckJoinForeignKeyMismatch(BooleanExpression condition)
        {
            var pairs = ExtractEqualityPairs(condition);

            foreach (var (leftCol, rightCol, comparison) in pairs)
            {
                var leftResolved = ResolveColumnToTable(leftCol);
                var rightResolved = ResolveColumnToTable(rightCol);

                if (leftResolved is null || rightResolved is null)
                {
                    continue;
                }

                var (leftTable, leftColumnName) = leftResolved.Value;
                var (rightTable, rightColumnName) = rightResolved.Value;

                CheckForeignKeyOnColumn(leftTable, leftColumnName, rightTable, rightColumnName, comparison);
                CheckForeignKeyOnColumn(rightTable, rightColumnName, leftTable, leftColumnName, comparison);
            }
        }

        private static List<(ColumnReferenceExpression Left, ColumnReferenceExpression Right, BooleanComparisonExpression Node)> ExtractEqualityPairs(
            BooleanExpression condition)
        {
            var results = new List<(ColumnReferenceExpression, ColumnReferenceExpression, BooleanComparisonExpression)>();
            CollectEqualityPairs(condition, results);
            return results;
        }

        private static void CollectEqualityPairs(
            BooleanExpression condition,
            List<(ColumnReferenceExpression, ColumnReferenceExpression, BooleanComparisonExpression)> results)
        {
            switch (condition)
            {
                case BooleanComparisonExpression { ComparisonType: BooleanComparisonType.Equals } comparison
                    when comparison.FirstExpression is ColumnReferenceExpression leftCol
                      && comparison.SecondExpression is ColumnReferenceExpression rightCol:
                    results.Add((leftCol, rightCol, comparison));
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

        private void CheckForeignKeyOnColumn(
            ResolvedTable sourceTable,
            string sourceColumnName,
            ResolvedTable joinedTable,
            string joinedColumnName,
            BooleanComparisonExpression diagnosticTarget)
        {
            var fks = GetForeignKeys(sourceTable);
            foreach (var fk in fks)
            {
                var idx = FindColumnIndex(fk.SourceColumns, sourceColumnName);
                if (idx < 0 || idx >= fk.TargetColumns.Count)
                {
                    continue;
                }

                if (!string.Equals(fk.TargetColumns[idx], joinedColumnName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TablesAreEqual(joinedTable, fk.TargetTable))
                {
                    AddDiagnostic(
                        fragment: diagnosticTarget,
                        message: $"Column '{sourceTable.SchemaName}.{sourceTable.TableName}.{sourceColumnName}' has a foreign key ('{fk.Name}') to '{fk.TargetTable.SchemaName}.{fk.TargetTable.TableName}.{fk.TargetColumns[idx]}', but this JOIN targets '{joinedTable.SchemaName}.{joinedTable.TableName}' instead.",
                        code: "join-foreign-key-mismatch",
                        category: "Schema",
                        fixable: false
                    );
                }
            }

            var refFks = GetReferencingForeignKeys(sourceTable);
            foreach (var fk in refFks)
            {
                var idx = FindColumnIndex(fk.TargetColumns, sourceColumnName);
                if (idx < 0 || idx >= fk.SourceColumns.Count)
                {
                    continue;
                }

                if (!string.Equals(fk.SourceColumns[idx], joinedColumnName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TablesAreEqual(joinedTable, fk.SourceTable))
                {
                    AddDiagnostic(
                        fragment: diagnosticTarget,
                        message: $"Column '{sourceTable.SchemaName}.{sourceTable.TableName}.{sourceColumnName}' is referenced by foreign key ('{fk.Name}') from '{fk.SourceTable.SchemaName}.{fk.SourceTable.TableName}.{fk.SourceColumns[idx]}', but this JOIN targets '{joinedTable.SchemaName}.{joinedTable.TableName}' instead.",
                        code: "join-foreign-key-mismatch",
                        category: "Schema",
                        fixable: false
                    );
                }
            }
        }

        private IReadOnlyList<SchemaForeignKeyInfo> GetForeignKeys(ResolvedTable table)
        {
            if (_foreignKeysCache.TryGetValue(table, out var cached))
            {
                return cached;
            }

            cached = schema.GetForeignKeys(table);
            _foreignKeysCache[table] = cached;
            return cached;
        }

        private IReadOnlyList<SchemaForeignKeyInfo> GetReferencingForeignKeys(ResolvedTable table)
        {
            if (_referencingForeignKeysCache.TryGetValue(table, out var cached))
            {
                return cached;
            }

            cached = schema.GetReferencingForeignKeys(table);
            _referencingForeignKeysCache[table] = cached;
            return cached;
        }

        private static int FindColumnIndex(IReadOnlyList<string> columns, string columnName)
        {
            for (var i = 0; i < columns.Count; i++)
            {
                if (string.Equals(columns[i], columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TablesAreEqual(ResolvedTable a, ResolvedTable b)
        {
            return string.Equals(a.DatabaseName, b.DatabaseName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.SchemaName, b.SchemaName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.TableName, b.TableName, StringComparison.OrdinalIgnoreCase);
        }

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

        private sealed class ResolvedTableKeyComparer : IEqualityComparer<ResolvedTable>
        {
            public static ResolvedTableKeyComparer Instance { get; } = new();

            public bool Equals(ResolvedTable? x, ResolvedTable? y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return TablesAreEqual(x, y);
            }

            public int GetHashCode(ResolvedTable obj)
            {
                var hash = new HashCode();
                hash.Add(obj.DatabaseName, StringComparer.OrdinalIgnoreCase);
                hash.Add(obj.SchemaName, StringComparer.OrdinalIgnoreCase);
                hash.Add(obj.TableName, StringComparer.OrdinalIgnoreCase);
                return hash.ToHashCode();
            }
        }
    }
}
