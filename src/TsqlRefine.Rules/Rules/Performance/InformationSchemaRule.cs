using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Prohibit INFORMATION_SCHEMA views; use sys catalog views for better performance
/// </summary>
public sealed class InformationSchemaRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-information-schema",
        Description: "Prohibit INFORMATION_SCHEMA views; use sys catalog views for better performance",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new InformationSchemaVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class InformationSchemaVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(NamedTableReference node)
        {
            // Check if the schema is INFORMATION_SCHEMA
            if (node.SchemaObject?.SchemaIdentifier?.Value != null &&
                node.SchemaObject.SchemaIdentifier.Value.Equals("INFORMATION_SCHEMA", StringComparison.OrdinalIgnoreCase))
            {
                AddDiagnostic(
                    fragment: node,
                    message: "INFORMATION_SCHEMA view found. Use sys catalog views instead for better performance.",
                    code: "avoid-information-schema",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
