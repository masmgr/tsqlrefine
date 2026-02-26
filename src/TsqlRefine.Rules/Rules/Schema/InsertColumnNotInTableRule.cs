using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects INSERT statements that reference columns not found in the target table.
/// </summary>
public sealed class InsertColumnNotInTableRule : SchemaAwareVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "insert-column-not-in-table",
        Description: "Detects INSERT statements that reference columns not found in the target table.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new InsertColumnNotInTableVisitor(context.Schema!);

    private sealed class InsertColumnNotInTableVisitor(ISchemaProvider schema) : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(InsertStatement node)
        {
            var insertSpec = node.InsertSpecification;
            var target = insertSpec?.Target as NamedTableReference;
            if (target is null || insertSpec is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var schemaObject = target.SchemaObject;
            var tableName = schemaObject.BaseIdentifier?.Value;
            if (tableName is null || tableName.StartsWith('#') || tableName.StartsWith('@'))
            {
                base.ExplicitVisit(node);
                return;
            }

            var schemaName = schemaObject.SchemaIdentifier?.Value;
            var dbName = schemaObject.DatabaseIdentifier?.Value;

            var resolvedTable = schema.ResolveTable(dbName, schemaName, tableName);
            if (resolvedTable is null)
            {
                // Table not found — skip (reported by unresolved-table-reference)
                base.ExplicitVisit(node);
                return;
            }

            var columns = insertSpec.Columns;
            if (columns is null or { Count: 0 })
            {
                base.ExplicitVisit(node);
                return;
            }

            foreach (var colRef in columns)
            {
                var columnName = colRef.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value;
                if (columnName is null)
                {
                    continue;
                }

                var resolvedColumn = schema.ResolveColumn(resolvedTable, columnName);
                if (resolvedColumn is null)
                {
                    AddDiagnostic(
                        fragment: colRef,
                        message: $"Column '{columnName}' not found in '{resolvedTable.SchemaName}.{resolvedTable.TableName}'.",
                        code: "insert-column-not-in-table",
                        category: "Schema",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
