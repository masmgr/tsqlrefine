using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Performance;

public sealed class ForbidTop100PercentOrderByRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "forbid-top-100-percent-order-by",
        Description: "Forbids TOP 100 PERCENT ORDER BY; it is redundant and often ignored by the optimizer.",
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

        var visitor = new ForbidTop100PercentOrderByVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class ForbidTop100PercentOrderByVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(QuerySpecification node)
        {
            // Check for TOP 100 PERCENT with ORDER BY
            if (node.TopRowFilter != null &&
                node.TopRowFilter.Percent &&
                node.OrderByClause != null)
            {
                // Check if the expression evaluates to 100
                if (node.TopRowFilter.Expression is IntegerLiteral intLiteral &&
                    intLiteral.Value == "100")
                {
                    AddDiagnostic(
                        fragment: node.TopRowFilter,
                        message: "TOP 100 PERCENT with ORDER BY is redundant; the optimizer often ignores it. Remove TOP 100 PERCENT or use a different approach.",
                        code: "forbid-top-100-percent-order-by",
                        category: "Performance",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
