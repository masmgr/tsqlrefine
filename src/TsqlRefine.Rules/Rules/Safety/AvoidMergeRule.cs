using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Safety;

public sealed class AvoidMergeRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-merge",
        Description: "Avoid using MERGE statement due to known bugs (see KB 3180087, KB 4519788)",
        Category: "Safety",
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

        var visitor = new AvoidMergeVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidMergeVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(MergeStatement node)
        {
            AddDiagnostic(
                fragment: node,
                message: "Avoid MERGE statement due to known bugs (see KB 3180087, KB 4519788). MERGE can cause data corruption, race conditions, and non-deterministic behavior. Use explicit INSERT, UPDATE, DELETE statements with proper transaction handling instead.",
                code: "avoid-merge",
                category: "Safety",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
