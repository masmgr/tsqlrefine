using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Prohibit linked server queries (4-part identifiers); use alternative data access patterns
/// </summary>
public sealed class LinkedServerRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-linked-server",
        Description: "Prohibit linked server queries (4-part identifiers); use alternative data access patterns",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new LinkedServerVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class LinkedServerVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SchemaObjectName node)
        {
            // Check if it's a 4-part identifier (server.database.schema.object)
            if (node.Identifiers.Count == 4)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "Linked server query found (4-part identifier). Consider using alternative data access patterns for better performance and reliability.",
                    code: "avoid-linked-server",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
