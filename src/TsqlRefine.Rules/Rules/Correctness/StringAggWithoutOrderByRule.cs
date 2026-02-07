using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

public sealed class StringAggWithoutOrderByRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "string-agg-without-order-by",
        Description: "Detects STRING_AGG without WITHIN GROUP (ORDER BY), which may produce non-deterministic string concatenation results.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // STRING_AGG is available in SQL Server 2017+ (CompatLevel 140+)
        if (context.CompatLevel < 140)
        {
            yield break;
        }

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new StringAggWithoutOrderByVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class StringAggWithoutOrderByVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(FunctionCall node)
        {
            // Check for STRING_AGG function
            if (node.FunctionName.Value.Equals("STRING_AGG", StringComparison.OrdinalIgnoreCase))
            {
                // Check if WITHIN GROUP (ORDER BY ...) clause is missing
                if (node.WithinGroupClause is null)
                {
                    AddDiagnostic(
                        fragment: node,
                        message: "STRING_AGG lacks WITHIN GROUP (ORDER BY ...); results may be non-deterministic.",
                        code: "string-agg-without-order-by",
                        category: "Correctness",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
