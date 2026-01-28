using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Transactions;

public sealed class TransactionWithoutCommitOrRollbackRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "transaction-without-commit-or-rollback",
        Description: "Detects BEGIN TRANSACTION statements without corresponding COMMIT or ROLLBACK in the same batch.",
        Category: "Transactions",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new TransactionWithoutCommitOrRollbackVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class TransactionWithoutCommitOrRollbackVisitor : DiagnosticVisitorBase
    {
        private readonly List<BeginTransactionStatement> _beginTransactions = new();
        private bool _hasCommitOrRollback;

        public override void ExplicitVisit(TSqlBatch node)
        {
            // Reset for each batch (GO separator creates new batch)
            _beginTransactions.Clear();
            _hasCommitOrRollback = false;

            base.ExplicitVisit(node);

            // After visiting the batch, check if any BEGIN TRAN lacks termination
            foreach (var beginTran in _beginTransactions)
            {
                if (!_hasCommitOrRollback)
                {
                    AddDiagnostic(
                        fragment: beginTran,
                        message: "BEGIN TRANSACTION without corresponding COMMIT or ROLLBACK in the same batch. Orphaned transactions hold locks indefinitely and cause blocking issues. Ensure all transaction paths have proper termination.",
                        code: "transaction-without-commit-or-rollback",
                        category: "Transactions",
                        fixable: false
                    );
                }
            }
        }

        public override void ExplicitVisit(BeginTransactionStatement node)
        {
            _beginTransactions.Add(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CommitTransactionStatement node)
        {
            _hasCommitOrRollback = true;
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(RollbackTransactionStatement node)
        {
            _hasCommitOrRollback = true;
            base.ExplicitVisit(node);
        }

        // Special handling for CREATE PROCEDURE/FUNCTION - analyze as separate scope
        public override void ExplicitVisit(CreateProcedureStatement node)
        {
            // Save parent batch state
            var parentBeginTransactions = new List<BeginTransactionStatement>(_beginTransactions);
            var parentHasCommitOrRollback = _hasCommitOrRollback;

            // Reset for procedure scope
            _beginTransactions.Clear();
            _hasCommitOrRollback = false;

            base.ExplicitVisit(node);

            // Check procedure scope
            foreach (var beginTran in _beginTransactions)
            {
                if (!_hasCommitOrRollback)
                {
                    AddDiagnostic(
                        fragment: beginTran,
                        message: "BEGIN TRANSACTION in stored procedure without COMMIT or ROLLBACK. Ensure all code paths properly terminate the transaction.",
                        code: "transaction-without-commit-or-rollback",
                        category: "Transactions",
                        fixable: false
                    );
                }
            }

            // Restore parent batch state
            _beginTransactions.Clear();
            _beginTransactions.AddRange(parentBeginTransactions);
            _hasCommitOrRollback = parentHasCommitOrRollback;
        }

        public override void ExplicitVisit(CreateFunctionStatement node)
        {
            // Similar handling for functions
            var parentBeginTransactions = new List<BeginTransactionStatement>(_beginTransactions);
            var parentHasCommitOrRollback = _hasCommitOrRollback;

            _beginTransactions.Clear();
            _hasCommitOrRollback = false;

            base.ExplicitVisit(node);

            foreach (var beginTran in _beginTransactions)
            {
                if (!_hasCommitOrRollback)
                {
                    AddDiagnostic(
                        fragment: beginTran,
                        message: "BEGIN TRANSACTION in function without COMMIT or ROLLBACK. Ensure all code paths properly terminate the transaction.",
                        code: "transaction-without-commit-or-rollback",
                        category: "Transactions",
                        fixable: false
                    );
                }
            }

            _beginTransactions.Clear();
            _beginTransactions.AddRange(parentBeginTransactions);
            _hasCommitOrRollback = parentHasCommitOrRollback;
        }
    }
}
