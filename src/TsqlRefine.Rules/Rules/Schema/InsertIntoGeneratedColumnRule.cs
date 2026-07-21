using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>Detects explicit INSERT writes to computed or identity columns.</summary>
public sealed class InsertIntoGeneratedColumnRule : SchemaAwareVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        "insert-into-generated-column",
        "Detects explicit INSERT writes to computed columns or identity columns without IDENTITY_INSERT.",
        "Schema",
        RuleSeverity.Error,
        false);

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new InsertIntoGeneratedColumnVisitor(context.Schema!);

    private sealed class InsertIntoGeneratedColumnVisitor(ISchemaProvider schema) : DiagnosticVisitorBase
    {
        private readonly HashSet<string> _identityInsertTables = new(StringComparer.OrdinalIgnoreCase);

        public override void ExplicitVisit(CreateProcedureStatement node) =>
            VisitProcedureScope(() => base.ExplicitVisit(node));

        public override void ExplicitVisit(CreateOrAlterProcedureStatement node) =>
            VisitProcedureScope(() => base.ExplicitVisit(node));

        public override void ExplicitVisit(AlterProcedureStatement node) =>
            VisitProcedureScope(() => base.ExplicitVisit(node));

        public override void ExplicitVisit(SetIdentityInsertStatement node)
        {
            var schemaObject = node.Table;
            var tableName = schemaObject?.BaseIdentifier?.Value;
            var table = schemaObject is null || string.IsNullOrWhiteSpace(tableName)
                ? null
                : schema.ResolveTable(
                    schemaObject.DatabaseIdentifier?.Value,
                    schemaObject.SchemaIdentifier?.Value,
                    tableName);
            if (table is not null)
            {
                var key = DmlWriteTargetHelpers.GetTableKey(table);
                if (node.IsOn)
                {
                    _identityInsertTables.Add(key);
                }
                else
                {
                    _identityInsertTables.Remove(key);
                }
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(InsertStatement node)
        {
            var specification = node.InsertSpecification;
            var table = DmlWriteTargetHelpers.ResolveNamedTarget(schema, specification?.Target);
            if (specification?.Columns is not { Count: > 0 } columns || table is null || table.IsView)
            {
                base.ExplicitVisit(node);
                return;
            }

            var identityInsertEnabled = _identityInsertTables.Contains(DmlWriteTargetHelpers.GetTableKey(table));
            foreach (var column in columns)
            {
                var name = DmlWriteTargetHelpers.GetColumnName(column);
                var resolved = name is null ? null : schema.ResolveColumn(table, name);
                if (resolved?.Column.IsComputed == true)
                {
                    AddDiagnostic(column, $"Computed column '{resolved.Column.Name}' cannot be assigned by INSERT.");
                }
                else if (resolved?.Column.IsIdentity == true && !identityInsertEnabled)
                {
                    AddDiagnostic(
                        column,
                        $"Identity column '{resolved.Column.Name}' requires SET IDENTITY_INSERT {table.SchemaName}.{table.TableName} ON.");
                }
            }

            base.ExplicitVisit(node);
        }

        private void VisitProcedureScope(Action visit)
        {
            var outerState = _identityInsertTables.ToArray();
            _identityInsertTables.Clear();
            visit();
            _identityInsertTables.Clear();
            _identityInsertTables.UnionWith(outerState);
        }
    }
}
