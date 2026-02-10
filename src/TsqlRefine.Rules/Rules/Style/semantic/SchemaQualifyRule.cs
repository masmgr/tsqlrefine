using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style.Semantic;

/// <summary>
/// Requires all table references to include schema qualification (e.g., dbo.Users) for clarity and to avoid ambiguity.
/// </summary>
public sealed class SchemaQualifyRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/schema-qualify",
        Description: "Requires all table references to include schema qualification (e.g., dbo.Users) for clarity and to avoid ambiguity.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new SchemaQualifyVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class SchemaQualifyVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(NamedTableReference node)
        {
            var schemaObject = node.SchemaObject;

            // Skip if schema is already specified
            if (schemaObject.SchemaIdentifier != null)
            {
                base.ExplicitVisit(node);
                return;
            }

            var tableName = schemaObject.BaseIdentifier.Value;

            // Skip temp tables (#temp, ##global)
            if (ScriptDomHelpers.IsTemporaryTableName(tableName))
            {
                base.ExplicitVisit(node);
                return;
            }

            // Skip table variables (@table)
            if (tableName.StartsWith('@'))
            {
                base.ExplicitVisit(node);
                return;
            }

            // Report unqualified table reference
            AddDiagnostic(
                fragment: schemaObject,
                message: $"Table reference '{tableName}' should include schema qualification (e.g., dbo.{tableName}) for clarity and to avoid naming conflicts.",
                code: "semantic/schema-qualify",
                category: "Style",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
