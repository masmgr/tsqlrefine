using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

public sealed class RequireTryCatchForTransactionRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-try-catch-for-transaction",
        Description: "Requires TRY/CATCH around explicit transactions to ensure errors trigger rollback and cleanup consistently.",
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

        var visitor = new RequireTryCatchForTransactionVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireTryCatchForTransactionVisitor : DiagnosticVisitorBase
    {
        private readonly Stack<bool> _tryCatchStack = new();

        public override void ExplicitVisit(TryCatchStatement node)
        {
            _tryCatchStack.Push(true);
            base.ExplicitVisit(node);
            _tryCatchStack.Pop();
        }

        public override void ExplicitVisit(BeginTransactionStatement node)
        {
            // Check if we're inside a TRY/CATCH block
            if (_tryCatchStack.Count == 0 || !_tryCatchStack.Peek())
            {
                AddDiagnostic(
                    fragment: node,
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
