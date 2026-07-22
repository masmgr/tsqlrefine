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
        new AvoidSelectStarVisitor();

    private sealed class AvoidSelectStarVisitor : DiagnosticVisitorBase
    {
        private int _existsDepth;
        private bool _allowTemporarySelectInto;

        public override void ExplicitVisit(SelectStatement node)
        {
            var previousAllowTemporarySelectInto = _allowTemporarySelectInto;
            _allowTemporarySelectInto = IsTemporarySelectInto(node);
            base.ExplicitVisit(node);
            _allowTemporarySelectInto = previousAllowTemporarySelectInto;
        }

        public override void ExplicitVisit(QueryDerivedTable node)
        {
            var previousAllowTemporarySelectInto = _allowTemporarySelectInto;
            _allowTemporarySelectInto = false;
            base.ExplicitVisit(node);
            _allowTemporarySelectInto = previousAllowTemporarySelectInto;
        }

        public override void ExplicitVisit(ScalarSubquery node)
        {
            var previousAllowTemporarySelectInto = _allowTemporarySelectInto;
            _allowTemporarySelectInto = false;
            base.ExplicitVisit(node);
            _allowTemporarySelectInto = previousAllowTemporarySelectInto;
        }

        public override void ExplicitVisit(CommonTableExpression node)
        {
            var previousAllowTemporarySelectInto = _allowTemporarySelectInto;
            _allowTemporarySelectInto = false;
            base.ExplicitVisit(node);
            _allowTemporarySelectInto = previousAllowTemporarySelectInto;
        }

        public override void ExplicitVisit(ExistsPredicate node)
        {
            _existsDepth++;
            base.ExplicitVisit(node);
            _existsDepth--;
        }

        public override void ExplicitVisit(SelectStarExpression node)
        {
            // Skip if inside EXISTS clause - SELECT * is acceptable there
            if (_existsDepth > 0 || _allowTemporarySelectInto)
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

            AddDiagnostic(node, "Avoid SELECT *; explicitly list required columns.");

            base.ExplicitVisit(node);
        }

        private static bool IsTemporarySelectInto(SelectStatement node) =>
            node.Into?.BaseIdentifier?.Value.StartsWith('#') is true;
    }
}
