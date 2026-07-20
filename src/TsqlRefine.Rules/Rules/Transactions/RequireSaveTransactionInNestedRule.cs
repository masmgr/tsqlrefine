using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Detects nested BEGIN TRANSACTION without SAVE TRANSACTION. Without a savepoint, ROLLBACK in a nested transaction rolls back the entire outer transaction.
/// </summary>
public sealed class RequireSaveTransactionInNestedRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "require-save-transaction-in-nested",
        Description: "Detects nested BEGIN TRANSACTION without SAVE TRANSACTION. Without a savepoint, ROLLBACK in a nested transaction rolls back the entire outer transaction.",
        Category: "Transactions",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new RequireSaveTransactionInNestedVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireSaveTransactionInNestedVisitor : ProcedureScopeDiagnosticVisitorBase
    {
        private readonly LinearTransactionState _transactions = new();

        public override void ExplicitVisit(TSqlBatch node)
        {
            _transactions.Reset();
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(BeginTransactionStatement node)
        {
            _transactions.Begin(node);

            if (_transactions.Depth > 1 && !_transactions.HasSavepoint)
            {
                AddDiagnostic(
                    range: ScriptDomHelpers.GetLeadingKeywordPairRange(node),
                    message: "Nested BEGIN TRANSACTION without SAVE TRANSACTION. Use SAVE TRANSACTION with a savepoint name before nesting transactions, otherwise ROLLBACK will roll back the entire outer transaction.",
                    code: "require-save-transaction-in-nested",
                    category: "Transactions",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SaveTransactionStatement node)
        {
            _transactions.Save();
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
            _transactions.Restore(parentState);
        }
    }
}
