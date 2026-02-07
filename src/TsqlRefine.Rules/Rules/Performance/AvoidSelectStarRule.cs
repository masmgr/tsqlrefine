using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

public sealed class AvoidSelectStarRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-select-star",
        Description: "Avoid SELECT * in queries.",
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

        var visitor = new AvoidSelectStarVisitor(Metadata);
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidSelectStarVisitor(RuleMetadata metadata) : DiagnosticVisitorBase
    {
        private int _existsDepth;

        public override void ExplicitVisit(ExistsPredicate node)
        {
            _existsDepth++;
            base.ExplicitVisit(node);
            _existsDepth--;
        }

        public override void ExplicitVisit(SelectStarExpression node)
        {
            // Skip if inside EXISTS clause - SELECT * is acceptable there
            if (_existsDepth > 0)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Skip qualified wildcards (e.g., t.* or dbo.users.*)
            if (node.Qualifier is not null)
            {
                base.ExplicitVisit(node);
                return;
            }

            AddDiagnostic(
                fragment: node,
                message: "Avoid SELECT *; explicitly list required columns.",
                code: metadata.RuleId,
                category: metadata.Category,
                fixable: metadata.Fixable
            );

            base.ExplicitVisit(node);
        }
    }
}
