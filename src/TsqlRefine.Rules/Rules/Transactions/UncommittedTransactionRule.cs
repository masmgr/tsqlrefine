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

        // Use a greedy matching algorithm: match each BEGIN with the first available COMMIT/ROLLBACK
        var usedCommits = new HashSet<int>();
        var usedRollbacks = new HashSet<int>();

        foreach (var beginTran in visitor.BeginTransactions)
        {
            var hasMatch = false;

            // Try to find an unused COMMIT after this BEGIN
            for (var i = 0; i < visitor.CommitTransactions.Count; i++)
            {
                if (!usedCommits.Contains(i) && visitor.CommitTransactions[i].StartLine > beginTran.StartLine)
                {
                    usedCommits.Add(i);
                    hasMatch = true;
                    break;
                }
            }

            // If no COMMIT found, try to find an unused ROLLBACK after this BEGIN
            if (!hasMatch)
            {
                for (var i = 0; i < visitor.RollbackTransactions.Count; i++)
                {
                    if (!usedRollbacks.Contains(i) && visitor.RollbackTransactions[i].StartLine > beginTran.StartLine)
                    {
                        usedRollbacks.Add(i);
                        hasMatch = true;
                        break;
                    }
                }
            }

            if (!hasMatch)
            {
                yield return RuleHelpers.CreateDiagnostic(
                    range: ScriptDomHelpers.GetRange(beginTran),
                    message: "BEGIN TRANSACTION without corresponding COMMIT TRANSACTION in the same file",
                    code: Metadata.RuleId,
                    category: Metadata.Category,
                    fixable: Metadata.Fixable
                );
            }
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class TransactionVisitor : TSqlFragmentVisitor
    {
        public List<BeginTransactionStatement> BeginTransactions { get; } = new();
        public List<CommitTransactionStatement> CommitTransactions { get; } = new();
        public List<RollbackTransactionStatement> RollbackTransactions { get; } = new();

        public override void ExplicitVisit(BeginTransactionStatement node)
        {
            BeginTransactions.Add(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(CommitTransactionStatement node)
        {
            CommitTransactions.Add(node);
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(RollbackTransactionStatement node)
        {
            RollbackTransactions.Add(node);
            base.ExplicitVisit(node);
        }
    }
}
