using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects LIKE patterns with a leading wildcard (%, _, [) in predicates, which prevents index usage and causes full table scans.
/// </summary>
public sealed class LikeLeadingWildcardRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "like-leading-wildcard",
        Description: "Detects LIKE patterns with a leading wildcard (%, _, [) in predicates, which prevents index usage and causes full table scans.",
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

        var visitor = new LikeLeadingWildcardVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class LikeLeadingWildcardVisitor : PredicateAwareVisitorBase
    {
        public override void ExplicitVisit(LikePredicate node)
        {
            if (IsInPredicate && node.SecondExpression is StringLiteral literal)
            {
                var pattern = literal.Value;
                if (pattern.Length > 0 && pattern[0] is '%' or '_' or '[')
                {
                    AddDiagnostic(
                        fragment: node,
                        message: $"LIKE pattern '{pattern}' starts with a leading wildcard, which prevents index usage and causes a full table/index scan. Consider using full-text search or restructuring the query to avoid leading wildcards.",
                        code: "like-leading-wildcard",
                        category: "Performance",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
