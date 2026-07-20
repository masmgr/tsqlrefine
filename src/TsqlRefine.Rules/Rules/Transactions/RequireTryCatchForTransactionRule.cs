using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Requires TRY/CATCH around explicit transactions to ensure errors trigger rollback and cleanup consistently.
/// </summary>
public sealed class RequireTryCatchForTransactionRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "require-try-catch-for-transaction",
        Description: "Requires TRY/CATCH around explicit transactions to ensure errors trigger rollback and cleanup consistently.",
        Category: "Transactions",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new RequireTryCatchForTransactionVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireTryCatchForTransactionVisitor : DiagnosticVisitorBase
    {
        private int _tryCatchDepth;

        public override void ExplicitVisit(TryCatchStatement node)
        {
            _tryCatchDepth++;
            node.TryStatements?.Accept(this);
            _tryCatchDepth--;

            // A transaction opened while handling an error is not protected by the TRY that
            // has already completed.
            node.CatchStatements?.Accept(this);
        }

        public override void ExplicitVisit(BeginTransactionStatement node)
        {
            // Check if we're inside a TRY/CATCH block
            if (_tryCatchDepth == 0)
            {
                AddDiagnostic(
                    range: ScriptDomHelpers.GetFirstTokenRange(node),
                    message: "BEGIN TRANSACTION should be wrapped in a TRY/CATCH block to ensure errors trigger rollback and cleanup.",
                    code: "require-try-catch-for-transaction",
                    category: "Transactions",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
