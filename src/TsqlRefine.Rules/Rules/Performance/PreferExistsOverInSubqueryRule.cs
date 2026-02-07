using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;
using TsqlRefine.Rules.Helpers.Diagnostics;
using TsqlRefine.Rules.Helpers.Visitors;

namespace TsqlRefine.Rules.Rules.Performance;

public sealed class PreferExistsOverInSubqueryRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "prefer-exists-over-in-subquery",
        Description: "Detects WHERE column IN (SELECT ...) patterns and recommends using EXISTS instead for better performance with large datasets.",
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

        var visitor = new PreferExistsOverInSubqueryVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class PreferExistsOverInSubqueryVisitor : PredicateAwareVisitorBase
    {
        public override void ExplicitVisit(InPredicate node)
        {
            // Only flag IN with subquery, not IN with value lists
            if (IsInPredicate && node.Subquery is not null)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "Consider using EXISTS instead of IN with a subquery. EXISTS can be more efficient for large datasets as it short-circuits once a match is found.",
                    code: "prefer-exists-over-in-subquery",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
