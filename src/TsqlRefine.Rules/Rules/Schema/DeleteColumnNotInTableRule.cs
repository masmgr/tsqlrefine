using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects DELETE statements whose WHERE clause references columns not found in the target table.
/// </summary>
public sealed class DeleteColumnNotInTableRule : SchemaAwareVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "delete-column-not-in-table",
        Description: "Detects DELETE statements whose WHERE clause references columns not found in the target table.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new DeleteColumnNotInTableVisitor(context.Schema!);

    private sealed class DeleteColumnNotInTableVisitor(ISchemaProvider schema) : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(DeleteStatement node)
        {
            var deleteSpec = node.DeleteSpecification;
            var target = deleteSpec?.Target as NamedTableReference;
            if (target is null || deleteSpec is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var schemaObject = target.SchemaObject;
            var tableName = schemaObject.BaseIdentifier?.Value;
            if (tableName is null || AliasMapBuilder.IsTemporaryOrVariable(tableName))
            {
                base.ExplicitVisit(node);
                return;
            }

            var schemaName = schemaObject.SchemaIdentifier?.Value;
            var dbName = schemaObject.DatabaseIdentifier?.Value;

            var resolvedTarget = ResolveTargetTable(deleteSpec, tableName, dbName, schemaName);
            if (resolvedTarget is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            if (deleteSpec.WhereClause is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            AliasMap? aliasMap = null;
            if (deleteSpec.FromClause?.TableReferences is { Count: > 0 } tableRefs)
            {
                aliasMap = AliasMapBuilder.Build(tableRefs, schema);
            }

            var checker = new ColumnChecker(schema, resolvedTarget, aliasMap);
            deleteSpec.WhereClause.Accept(checker);

            foreach (var diagnostic in checker.Diagnostics)
            {
                AddDiagnostic(diagnostic);
            }

            base.ExplicitVisit(node);
        }

        private ResolvedTable? ResolveTargetTable(
            DeleteSpecification deleteSpec,
            string tableName,
            string? dbName,
            string? schemaName)
        {
            if (dbName is not null || schemaName is not null)
            {
                return schema.ResolveTable(dbName, schemaName, tableName);
            }

            if (deleteSpec.FromClause?.TableReferences is { Count: > 0 } tableRefs)
            {
                var aliasMap = AliasMapBuilder.Build(tableRefs, schema);
                if (aliasMap.TryResolve(tableName, out var mapped))
                {
                    return mapped;
                }
            }

            return schema.ResolveTable(null, null, tableName);
        }
    }

    /// <summary>
    /// Validates column references within a WHERE clause against resolved tables in scope.
    /// Stops descent into subqueries to avoid false positives on independent scopes.
    /// </summary>
    private sealed class ColumnChecker(
        ISchemaProvider schema,
        ResolvedTable targetTable,
        AliasMap? aliasMap) : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(ScalarSubquery node)
        {
            // Do not descend into subqueries — they have their own scope.
        }

        public override void ExplicitVisit(QuerySpecification node)
        {
            // Do not descend into subqueries — they have their own scope.
        }

        public override void ExplicitVisit(ColumnReferenceExpression node)
        {
            if (node.ColumnType == ColumnType.Wildcard)
            {
                return;
            }

            var identifiers = node.MultiPartIdentifier?.Identifiers;
            if (identifiers is null or { Count: 0 })
            {
                return;
            }

            if (identifiers.Count >= 2)
            {
                ValidateQualifiedColumn(node, identifiers);
            }
            else
            {
                ValidateUnqualifiedColumn(node, identifiers[0].Value);
            }
        }

        private void ValidateQualifiedColumn(ColumnReferenceExpression node, IList<Identifier> identifiers)
        {
            var columnName = identifiers[identifiers.Count - 1].Value;

            if (aliasMap is not null && QualifierLookupKeyBuilder.TryResolve(aliasMap, identifiers, out var resolvedTable))
            {
                if (resolvedTable is null)
                {
                    // Unresolvable (CTE, derived table, temp table)
                    return;
                }

                if (schema.ResolveColumn(resolvedTable, columnName) is null)
                {
                    AddDiagnostic(
                        fragment: node,
                        message: $"Column '{columnName}' not found in '{resolvedTable.SchemaName}.{resolvedTable.TableName}'.",
                        code: "delete-column-not-in-table",
                        category: "Schema",
                        fixable: false);
                }

                return;
            }

            // No alias map or qualifier not found — check against target table
            if (schema.ResolveColumn(targetTable, columnName) is null)
            {
                AddDiagnostic(
                    fragment: node,
                    message: $"Column '{columnName}' not found in '{targetTable.SchemaName}.{targetTable.TableName}'.",
                    code: "delete-column-not-in-table",
                    category: "Schema",
                    fixable: false);
            }
        }

        private void ValidateUnqualifiedColumn(ColumnReferenceExpression node, string columnName)
        {
            if (aliasMap is not null)
            {
                // Multi-table DELETE: check all tables in scope
                foreach (var table in aliasMap.AllTables)
                {
                    if (schema.ResolveColumn(table, columnName) is not null)
                    {
                        return;
                    }
                }

                // If AllTables is empty (all unresolvable), skip
                if (aliasMap.AllTables.Count == 0)
                {
                    return;
                }
            }
            else
            {
                // Simple DELETE: check target table only
                if (schema.ResolveColumn(targetTable, columnName) is not null)
                {
                    return;
                }
            }

            AddDiagnostic(
                fragment: node,
                message: $"Column '{columnName}' not found in any table in the current scope.",
                code: "delete-column-not-in-table",
                category: "Schema",
                fixable: false);
        }

    }
}
