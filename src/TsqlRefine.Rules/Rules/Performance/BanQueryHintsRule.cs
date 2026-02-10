using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects query hints and table hints that bypass the optimizer, causing long-term maintenance issues.
/// </summary>
public sealed class BanQueryHintsRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "ban-query-hints",
        Description: "Detects query hints and table hints that bypass the optimizer, causing long-term maintenance issues.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new BanQueryHintsVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class BanQueryHintsVisitor : DiagnosticVisitorBase
    {
        // Check for table hints (WITH clause)
        public override void ExplicitVisit(TableHint node)
        {
            // Skip NOLOCK - it's handled by avoid-nolock rule
            if (node.HintKind == TableHintKind.NoLock || node.HintKind == TableHintKind.ReadUncommitted)
            {
                base.ExplicitVisit(node);
                return;
            }

            AddDiagnostic(
                fragment: node,
                message: $"Table hint '{node.HintKind}' forces a specific execution strategy and can cause performance issues when data distributions change. Consider removing hints and letting the optimizer choose.",
                code: "ban-query-hints",
                category: "Performance",
                fixable: false
            );

            base.ExplicitVisit(node);
        }

        // Check for OPTION clause (query-level optimizer hints)
        public override void ExplicitVisit(OptimizerHint node)
        {
            AddDiagnostic(
                fragment: node,
                message: $"Query hint '{node.GetType().Name}' bypasses the optimizer and can become technical debt. Statistics and indexes change, but hints persist, often degrading performance over time.",
                code: "ban-query-hints",
                category: "Performance",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
