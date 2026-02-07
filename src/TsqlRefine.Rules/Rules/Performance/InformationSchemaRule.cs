using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

public sealed class InformationSchemaRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "information-schema",
        Description: "Prohibit INFORMATION_SCHEMA views; use sys catalog views for better performance",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new InformationSchemaVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
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
                    code: "information-schema",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
