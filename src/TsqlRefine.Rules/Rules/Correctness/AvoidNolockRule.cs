using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Avoid using NOLOCK hint or READ UNCOMMITTED isolation level
/// </summary>
public sealed class AvoidNolockRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-nolock",
        Description: "Avoid using NOLOCK hint or READ UNCOMMITTED isolation level",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidNolockVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidNolockVisitor : DiagnosticVisitorBase
    {

        public override void ExplicitVisit(TableHint node)
        {
            if (node.HintKind == TableHintKind.NoLock)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "NOLOCK hint can lead to dirty reads and data inconsistency. Consider using SNAPSHOT isolation or explicit locking.",
                    code: "avoid-nolock",
                    category: "Correctness",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SetTransactionIsolationLevelStatement node)
        {
            if (node.Level == IsolationLevel.ReadUncommitted)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "READ UNCOMMITTED isolation level can lead to dirty reads and data inconsistency. Consider using SNAPSHOT or READ COMMITTED isolation.",
                    code: "avoid-nolock",
                    category: "Correctness",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
