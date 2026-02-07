using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

public sealed class RequireSaveTransactionInNestedRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-save-transaction-in-nested",
        Description: "Detects nested BEGIN TRANSACTION without SAVE TRANSACTION. Without a savepoint, ROLLBACK in a nested transaction rolls back the entire outer transaction.",
        Category: "Transactions",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new RequireSaveTransactionInNestedVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireSaveTransactionInNestedVisitor : DiagnosticVisitorBase
    {
        private int _transactionDepth;
        private bool _hasSaveTransaction;

        public override void ExplicitVisit(TSqlBatch node)
        {
            _transactionDepth = 0;
            _hasSaveTransaction = false;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(BeginTransactionStatement node)
        {
            _transactionDepth++;

            if (_transactionDepth > 1 && !_hasSaveTransaction)
            {
                AddDiagnostic(
                    fragment: node,
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
            _hasSaveTransaction = true;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CommitTransactionStatement node)
        {
            if (_transactionDepth > 0)
            {
                _transactionDepth--;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(RollbackTransactionStatement node)
        {
            if (_transactionDepth > 0)
            {
                _transactionDepth--;
            }

            base.ExplicitVisit(node);
        }

        // Handle CREATE PROCEDURE as separate scope
        public override void ExplicitVisit(CreateProcedureStatement node)
        {
            var parentDepth = _transactionDepth;
            var parentHasSave = _hasSaveTransaction;

            _transactionDepth = 0;
            _hasSaveTransaction = false;

            base.ExplicitVisit(node);

            _transactionDepth = parentDepth;
            _hasSaveTransaction = parentHasSave;
        }
    }
}
