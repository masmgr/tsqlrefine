using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Correctness;

public sealed class NoTopWithoutOrderByInSelectIntoRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "no-top-without-order-by-in-select-into",
        Description: "Detects SELECT TOP ... INTO without ORDER BY, which creates permanent tables with non-deterministic data.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new NoTopWithoutOrderByInSelectIntoVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class NoTopWithoutOrderByInSelectIntoVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SelectStatement node)
        {
            var querySpec = node.QueryExpression as QuerySpecification;

            // Check if this is SELECT TOP ... INTO without ORDER BY
            if (querySpec != null &&
                querySpec.TopRowFilter != null &&
                node.Into != null &&
                querySpec.OrderByClause == null)
            {
                AddDiagnostic(
                    fragment: querySpec.TopRowFilter,
                    message: "SELECT TOP ... INTO without ORDER BY creates a permanent table with non-deterministic data. Add an ORDER BY clause to ensure reproducible results.",
                    code: "no-top-without-order-by-in-select-into",
                    category: "Correctness",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
