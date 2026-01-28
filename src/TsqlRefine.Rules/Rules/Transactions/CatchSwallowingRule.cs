using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Transactions;

public sealed class CatchSwallowingRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "catch-swallowing",
        Description: "Detects CATCH blocks that suppress errors without proper logging or rethrowing, creating silent failures.",
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

        var visitor = new CatchSwallowingVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class CatchSwallowingVisitor : DiagnosticVisitorBase
    {
        private bool _insideTryCatchStatement;
        private bool _insideTryBlock;
        private bool _catchHasErrorPropagation;
        private TryCatchStatement? _currentTryCatch;

        public override void ExplicitVisit(TryCatchStatement node)
        {
            var wasInside = _insideTryCatchStatement;
            _insideTryCatchStatement = true;
            _currentTryCatch = node;

            // Visit TRY block first
            _insideTryBlock = true;
            _catchHasErrorPropagation = false;
            if (node.TryStatements != null)
            {
                node.TryStatements.Accept(this);
            }
            _insideTryBlock = false;

            // Visit CATCH blocks - check if any has THROW/RAISERROR
            _catchHasErrorPropagation = false;
            base.ExplicitVisit(node);

            // After visiting entire TRY/CATCH, check if CATCH had error propagation
            if (!_catchHasErrorPropagation && !wasInside)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "CATCH block suppresses errors without THROW or RAISERROR. This creates silent failures that are difficult to debug. Consider rethrowing the error or logging to a persistent store.",
                    code: "catch-swallowing",
                    category: "Transactions",
                    fixable: false
                );
            }

            _insideTryCatchStatement = wasInside;
            if (!wasInside)
            {
                _currentTryCatch = null;
            }
        }

        public override void ExplicitVisit(ThrowStatement node)
        {
            if (_insideTryCatchStatement && !_insideTryBlock)
            {
                _catchHasErrorPropagation = true;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(RaiseErrorStatement node)
        {
            if (_insideTryCatchStatement && !_insideTryBlock)
            {
                _catchHasErrorPropagation = true;
            }

            base.ExplicitVisit(node);
        }
    }
}
