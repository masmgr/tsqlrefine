using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Avoid SELECT * in queries.
/// </summary>
public sealed class AvoidSelectStarRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-select-star",
        Description: "Avoid SELECT * in queries.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidSelectStarVisitor(Metadata);

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
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
