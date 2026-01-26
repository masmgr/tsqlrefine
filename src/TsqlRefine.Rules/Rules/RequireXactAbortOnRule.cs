using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class RequireXactAbortOnRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-xact-abort-on",
        Description: "Requires SET XACT_ABORT ON with explicit transactions to ensure runtime errors reliably abort and roll back work.",
        Category: "Transaction Safety",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null || context.Ast.Fragment is not TSqlScript script)
        {
            yield break;
        }

        var visitor = new RequireXactAbortOnVisitor(script);
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireXactAbortOnVisitor : DiagnosticVisitorBase
    {
        private readonly TSqlScript _script;
        private bool _hasXactAbortOn = false;

        public RequireXactAbortOnVisitor(TSqlScript script)
        {
            _script = script;
        }

        public override void ExplicitVisit(PredicateSetStatement node)
        {
            // Check for SET XACT_ABORT ON
            if (node.Options == SetOptions.XactAbort && node.IsOn)
            {
                _hasXactAbortOn = true;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(BeginTransactionStatement node)
        {
            // Check if XACT_ABORT ON was set before this transaction
            if (!_hasXactAbortOn)
            {
                // Check if SET XACT_ABORT ON appears before this statement
                if (!HasXactAbortBeforeStatement(node))
                {
                    AddDiagnostic(
                        fragment: node,
                        message: "BEGIN TRANSACTION should be preceded by SET XACT_ABORT ON to ensure runtime errors reliably abort the transaction.",
                        code: "require-xact-abort-on",
                        category: "Transaction Safety",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }

        private bool HasXactAbortBeforeStatement(BeginTransactionStatement transactionStmt)
        {
            // Check batches before the transaction statement
            if (_script.Batches != null)
            {
                foreach (var batch in _script.Batches)
                {
                    if (batch.Statements != null)
                    {
                        foreach (var stmt in batch.Statements)
                        {
                            // If we reached the transaction statement, stop
                            if (stmt == transactionStmt)
                            {
                                return false;
                            }

                            // Check if this is SET XACT_ABORT ON
                            if (stmt is PredicateSetStatement predicateSet &&
                                predicateSet.Options == SetOptions.XactAbort &&
                                predicateSet.IsOn)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }
}
