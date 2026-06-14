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
        private SchemaColumnResolver? _resolver;
        private readonly Dictionary<ResolvedTable, IReadOnlyList<SchemaForeignKeyInfo>> _foreignKeysCache =
            new(ResolvedTableComparers.TableKeyComparer.Instance);
        private readonly Dictionary<ResolvedTable, IReadOnlyList<SchemaForeignKeyInfo>> _referencingForeignKeysCache =
            new(ResolvedTableComparers.TableKeyComparer.Instance);

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.FromClause?.TableReferences is { Count: > 0 } tableRefs)
            {
                var previousResolver = _resolver;

                _resolver = new SchemaColumnResolver(schema, AliasMapBuilder.Build(tableRefs, schema));
                node.FromClause.Accept(this);

                _resolver = previousResolver;
                return;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(QualifiedJoin node)
        {
            if (_resolver is not null && node.SearchCondition is not null)
            {
                CheckJoinForeignKeyMismatch(node.SearchCondition);
            }

            base.ExplicitVisit(node);
        }

        private void CheckJoinForeignKeyMismatch(BooleanExpression condition)
        {
            var pairs = JoinEqualityPairCollector.Extract(condition);

            foreach (var (leftCol, rightCol, comparison) in pairs)
            {
                var leftResolved = _resolver!.ResolveColumnToTable(leftCol);
                var rightResolved = _resolver.ResolveColumnToTable(rightCol);

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

                if (!ResolvedTableComparers.TablesAreEqual(joinedTable, fk.TargetTable))
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

                if (!ResolvedTableComparers.TablesAreEqual(joinedTable, fk.SourceTable))
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
    }
}
