using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Performance;

public sealed class DisallowSelectIntoRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "disallow-select-into",
        Description: "Warns on SELECT ... INTO; it implicitly creates schema and can produce fragile, environment-dependent results.",
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

        var visitor = new DisallowSelectIntoVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DisallowSelectIntoVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SelectStatement node)
        {
            if (node.Into != null)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "Avoid SELECT...INTO; it implicitly creates schema and can produce environment-dependent results. Use CREATE TABLE + INSERT instead.",
                    code: "disallow-select-into",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
