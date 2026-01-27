using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Performance;

public sealed class LinkedServerRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "linked-server",
        Description: "Prohibit linked server queries (4-part identifiers); use alternative data access patterns",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new LinkedServerVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
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
                    code: "linked-server",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
