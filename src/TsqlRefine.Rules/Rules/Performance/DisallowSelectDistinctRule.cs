using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

public sealed class DisallowSelectDistinctRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "disallow-select-distinct",
        Description: "Flags SELECT DISTINCT usage which often masks JOIN bugs or missing GROUP BY, and has performance implications.",
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

        var visitor = new DisallowSelectDistinctVisitor(Metadata);
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DisallowSelectDistinctVisitor(RuleMetadata metadata) : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.UniqueRowFilter == UniqueRowFilter.Distinct)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "SELECT DISTINCT often masks JOIN bugs or missing GROUP BY, and adds implicit sort/hash operations. Consider using GROUP BY or fixing JOIN logic instead.",
                    code: metadata.RuleId,
                    category: metadata.Category,
                    fixable: metadata.Fixable
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
