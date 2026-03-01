using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects references to tables or views that do not exist in the schema snapshot.
/// </summary>
public sealed class UnresolvedTableReferenceRule : SchemaAwareVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "unresolved-table-reference",
        Description: "Detects references to tables or views that do not exist in the schema snapshot.",
        Category: "Schema",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new UnresolvedTableReferenceVisitor(context.Schema!);

    private sealed class UnresolvedTableReferenceVisitor(ISchemaProvider schema) : DiagnosticVisitorBase
    {
        private static readonly FrozenSet<string> ExcludedSchemas =
            FrozenSet.ToFrozenSet(["sys", "INFORMATION_SCHEMA"], StringComparer.OrdinalIgnoreCase);

        public override void ExplicitVisit(NamedTableReference node)
        {
            var schemaObject = node.SchemaObject;
            var tableName = schemaObject.BaseIdentifier?.Value;
            if (tableName is null)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Skip temp tables and table variables
            if (tableName.StartsWith('#') || tableName.StartsWith('@'))
            {
                base.ExplicitVisit(node);
                return;
            }

            // Skip system schemas
            var schemaName = schemaObject.SchemaIdentifier?.Value;
            if (schemaName is not null && ExcludedSchemas.Contains(schemaName))
            {
                base.ExplicitVisit(node);
                return;
            }

            var dbName = schemaObject.DatabaseIdentifier?.Value;
            var resolved = schema.ResolveTable(dbName, schemaName, tableName);

            if (resolved is null)
            {
                var fullName = FormatTableName(dbName, schemaName, tableName);
                AddDiagnostic(
                    fragment: node,
                    message: $"Table or view '{fullName}' not found in schema snapshot.",
                    code: "unresolved-table-reference",
                    category: "Schema",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        private static string FormatTableName(string? db, string? schema, string name)
        {
            if (db is not null)
            {
                return $"{db}.{schema ?? "dbo"}.{name}";
            }

            return schema is not null ? $"{schema}.{name}" : name;
        }
    }
}
