using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Correctness;

public sealed class AvoidNolockRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-nolock",
        Description: "Avoid using NOLOCK hint or READ UNCOMMITTED isolation level",
        Category: "Correctness",
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

        var visitor = new AvoidNolockVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
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
