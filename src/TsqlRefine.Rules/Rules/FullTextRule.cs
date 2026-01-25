using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class FullTextRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "full-text",
        Description: "Prohibit full-text search predicates; use alternative search strategies for better performance",
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

        var visitor = new FullTextVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class FullTextVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(FullTextPredicate node)
        {
            AddDiagnostic(
                fragment: node,
                message: "Full-text search predicate found (CONTAINS/FREETEXT). Consider using alternative search strategies for better performance.",
                code: "full-text",
                category: "Performance",
                fixable: false
            );

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(FullTextTableReference node)
        {
            AddDiagnostic(
                fragment: node,
                message: "Full-text table function found (CONTAINSTABLE/FREETEXTTABLE). Consider using alternative search strategies for better performance.",
                code: "full-text",
                category: "Performance",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
