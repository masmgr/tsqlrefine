using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Detects BEGIN TRANSACTION statements without corresponding COMMIT or ROLLBACK in the same batch.
/// </summary>
public sealed class TransactionWithoutCommitOrRollbackRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-transaction-without-commit",
        Description: "Detects BEGIN TRANSACTION statements without corresponding COMMIT or ROLLBACK in the same batch.",
        Category: "Transactions",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new TransactionWithoutCommitOrRollbackVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class TransactionWithoutCommitOrRollbackVisitor : ProcedureScopeDiagnosticVisitorBase
    {
        private readonly LinearTransactionState _transactions = new();

        public override void ExplicitVisit(TSqlBatch node)
        {
            // Reset for each batch (GO separator creates new batch)
            _transactions.Reset();

            base.ExplicitVisit(node);
            ReportOpenTransactions(
                "BEGIN TRANSACTION without corresponding COMMIT or ROLLBACK in the same batch. Orphaned transactions hold locks indefinitely and cause blocking issues. Ensure all transaction paths have proper termination.");
        }

        public override void ExplicitVisit(BeginTransactionStatement node)
        {
            _transactions.Begin(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CommitTransactionStatement node)
        {
            _transactions.Commit();
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(RollbackTransactionStatement node)
        {
            _transactions.Rollback(node);
            base.ExplicitVisit(node);
        }

        protected override void VisitProcedureScope(Action visitChildren)
        {
            var parentState = _transactions.Capture();
            _transactions.Reset();

            visitChildren();

            ReportOpenTransactions(
                "BEGIN TRANSACTION in stored procedure without COMMIT or ROLLBACK. Ensure all code paths properly terminate the transaction.");
            _transactions.Restore(parentState);
        }

        private void ReportOpenTransactions(string message)
        {
            foreach (var beginTransaction in _transactions.OpenTransactions)
            {
                AddDiagnostic(
                    range: ScriptDomHelpers.GetLeadingKeywordPairRange(beginTransaction),
                    message: message,
                    code: "avoid-transaction-without-commit",
                    category: "Transactions",
                    fixable: false
                );
            }
        }
    }
}
