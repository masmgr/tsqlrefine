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

    private sealed class TransactionWithoutCommitOrRollbackVisitor : DiagnosticVisitorBase
    {
        private readonly Stack<BeginTransactionStatement> _openTransactions = new();

        public override void ExplicitVisit(TSqlBatch node)
        {
            // Reset for each batch (GO separator creates new batch)
            _openTransactions.Clear();

            base.ExplicitVisit(node);
            ReportOpenTransactions(
                "BEGIN TRANSACTION without corresponding COMMIT or ROLLBACK in the same batch. Orphaned transactions hold locks indefinitely and cause blocking issues. Ensure all transaction paths have proper termination.");
        }

        public override void ExplicitVisit(BeginTransactionStatement node)
        {
            _openTransactions.Push(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CommitTransactionStatement node)
        {
            if (_openTransactions.Count > 0)
            {
                _openTransactions.Pop();
            }
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(RollbackTransactionStatement node)
        {
            if (node.Name is null)
            {
                _openTransactions.Clear();
            }
            base.ExplicitVisit(node);
        }

        // Analyze stored procedure bodies as scopes independent from their containing batch.
        public override void ExplicitVisit(CreateProcedureStatement node)
        {
            var parentOpenTransactions = _openTransactions.Reverse().ToArray();

            _openTransactions.Clear();

            base.ExplicitVisit(node);

            ReportOpenTransactions(
                "BEGIN TRANSACTION in stored procedure without COMMIT or ROLLBACK. Ensure all code paths properly terminate the transaction.");

            _openTransactions.Clear();
            foreach (var beginTransaction in parentOpenTransactions)
            {
                _openTransactions.Push(beginTransaction);
            }
        }

        private void ReportOpenTransactions(string message)
        {
            foreach (var beginTransaction in _openTransactions.Reverse())
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
