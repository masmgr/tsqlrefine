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
        private SchemaColumnResolver? _resolver;
        private ResolvedTable? _currentTarget;

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
            if (tableName is null || AliasMapBuilder.IsTemporaryOrVariable(tableName))
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

            var previousResolver = _resolver;
            var previousTarget = _currentTarget;

            _resolver = new SchemaColumnResolver(schema, aliasMap);
            _currentTarget = resolvedTarget;

            foreach (var tableRef in tableRefs)
            {
                TraverseJoins(tableRef);
            }

            _resolver = previousResolver;
            _currentTarget = previousTarget;

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
            var pairs = JoinEqualityPairCollector.Extract(joinNode.SearchCondition);
            if (pairs.Count == 0)
            {
                return;
            }

            var targetColumns = new List<string>();
            var joinedColumns = new List<string>();
            ResolvedTable? joinedTable = null;

            foreach (var (leftCol, rightCol, _) in pairs)
            {
                var leftResolved = _resolver!.ResolveColumnToTable(leftCol);
                var rightResolved = _resolver.ResolveColumnToTable(rightCol);
                if (leftResolved is null || rightResolved is null)
                {
                    continue;
                }

                var (leftTable, leftColName) = leftResolved.Value;
                var (rightTable, rightColName) = rightResolved.Value;

                if (ResolvedTableComparers.TablesAreEqual(leftTable, _currentTarget!))
                {
                    if (joinedTable is not null && !ResolvedTableComparers.TablesAreEqual(rightTable, joinedTable))
                    {
                        continue;
                    }

                    targetColumns.Add(leftColName);
                    joinedColumns.Add(rightColName);
                    joinedTable ??= rightTable;
                }
                else if (ResolvedTableComparers.TablesAreEqual(rightTable, _currentTarget!))
                {
                    if (joinedTable is not null && !ResolvedTableComparers.TablesAreEqual(leftTable, joinedTable))
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
    }
}
