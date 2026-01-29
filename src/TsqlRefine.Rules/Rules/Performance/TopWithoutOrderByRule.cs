using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Performance;

public sealed class TopWithoutOrderByRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "top-without-order-by",
        Description: "Detects TOP clause without ORDER BY, which produces non-deterministic results.",
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

        var visitor = new TopWithoutOrderByVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class TopWithoutOrderByVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SelectStatement node)
        {
            var querySpec = node.QueryExpression as QuerySpecification;
            if (querySpec != null &&
                querySpec.TopRowFilter != null &&
                querySpec.OrderByClause == null)
            {
                AddDiagnostic(
                    fragment: querySpec.TopRowFilter,
                    message: "TOP clause without ORDER BY produces non-deterministic results. Add an ORDER BY clause to ensure consistent results.",
                    code: "top-without-order-by",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
