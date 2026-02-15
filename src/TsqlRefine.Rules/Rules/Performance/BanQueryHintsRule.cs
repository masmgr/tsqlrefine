using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects query hints and table hints that bypass the optimizer, causing long-term maintenance issues.
/// </summary>
public sealed class BanQueryHintsRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-query-hints",
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
        private static readonly HashSet<TableHintKind> BannedTableHintKinds =
        [
            TableHintKind.ForceSeek,
            TableHintKind.ForceScan,
            TableHintKind.Index,
            TableHintKind.NoExpand,
            TableHintKind.FastFirstRow,
            TableHintKind.SpatialWindowMaxCells
        ];

        private static readonly HashSet<OptimizerHintKind> ExcludedOptimizerHintKinds =
        [
            // Common operational hints frequently used in production:
            // keep noise low and focus this rule on optimizer-forcing hints.
            OptimizerHintKind.Recompile,
            OptimizerHintKind.OptimizeFor,
            OptimizerHintKind.MaxRecursion,
            OptimizerHintKind.Label,
            // OPTION (TABLE HINT(...)) is evaluated via each nested TableHint.
            OptimizerHintKind.TableHints
        ];

        // Base table hint handler.
        public override void ExplicitVisit(TableHint node)
        {
            ReportTableHintIfNeeded(node, node.HintKind);
            base.ExplicitVisit(node);
        }

        // Derived table hints such as INDEX(...) and FORCESEEK are not always visited via TableHint.
        public override void ExplicitVisit(LiteralTableHint node)
        {
            ReportTableHintIfNeeded(node, node.HintKind);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(IndexTableHint node)
        {
            ReportTableHintIfNeeded(node, node.HintKind);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(ForceSeekTableHint node)
        {
            ReportTableHintIfNeeded(node, node.HintKind);
            base.ExplicitVisit(node);
        }

        // Check OPTION(...) query-level hints.
        public override void ExplicitVisit(OptimizerHint node)
        {
            ReportOptimizerHintIfNeeded(node, node.HintKind);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(LiteralOptimizerHint node)
        {
            ReportOptimizerHintIfNeeded(node, node.HintKind);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(OptimizeForOptimizerHint node)
        {
            ReportOptimizerHintIfNeeded(node, node.HintKind);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(TableHintsOptimizerHint node)
        {
            ReportOptimizerHintIfNeeded(node, node.HintKind);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(UseHintList node)
        {
            ReportOptimizerHintIfNeeded(node, node.HintKind);
            base.ExplicitVisit(node);
        }

        private static bool ShouldReportTableHint(TableHintKind hintKind) =>
            BannedTableHintKinds.Contains(hintKind);

        private void ReportTableHintIfNeeded(TSqlFragment node, TableHintKind hintKind)
        {
            if (!ShouldReportTableHint(hintKind))
            {
                return;
            }

            AddDiagnostic(
                fragment: node,
                message: $"Table hint '{hintKind}' constrains optimizer choices and can become long-term technical debt. Consider removing the hint and validating indexes/statistics instead.",
                code: "avoid-query-hints",
                category: "Performance",
                fixable: false
            );
        }

        private void ReportOptimizerHintIfNeeded(TSqlFragment node, OptimizerHintKind hintKind)
        {
            if (ExcludedOptimizerHintKinds.Contains(hintKind))
            {
                return;
            }

            AddDiagnostic(
                fragment: node,
                message: $"Query hint '{hintKind}' bypasses optimizer decisions and can become technical debt as data and indexes evolve.",
                code: "avoid-query-hints",
                category: "Performance",
                fixable: false
            );
        }
    }
}
