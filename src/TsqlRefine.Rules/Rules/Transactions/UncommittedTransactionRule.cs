using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Rule that detects BEGIN TRANSACTION statements without corresponding COMMIT TRANSACTION in the same file.
/// </summary>
public sealed class UncommittedTransactionRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "uncommitted-transaction",
        Description: "BEGIN TRANSACTION requires corresponding COMMIT TRANSACTION in the same file",
        Category: "Transactions",
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

        var visitor = new TransactionVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var beginTran in visitor.OpenTransactions)
        {
            yield return RuleHelpers.CreateDiagnostic(
                range: ScriptDomHelpers.GetLeadingKeywordPairRange(beginTran),
                message: "BEGIN TRANSACTION without corresponding COMMIT TRANSACTION in the same file",
                code: Metadata.RuleId,
                category: Metadata.Category,
                fixable: Metadata.Fixable
            );
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class TransactionVisitor : TSqlFragmentVisitor
    {
        private readonly LinearTransactionState _transactions = new();

        internal IReadOnlyList<BeginTransactionStatement> OpenTransactions => _transactions.OpenTransactions;

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
    }
}
