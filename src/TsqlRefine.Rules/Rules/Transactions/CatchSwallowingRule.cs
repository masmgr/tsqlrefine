using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Detects CATCH blocks that suppress errors without proper logging or rethrowing, creating silent failures.
/// </summary>
public sealed class CatchSwallowingRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-catch-swallowing",
        Description: "Detects CATCH blocks that suppress errors without proper logging or rethrowing, creating silent failures.",
        Category: "Transactions",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new CatchSwallowingVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class CatchSwallowingVisitor : DiagnosticVisitorBase
    {
        private int _catchDepth;
        private bool _catchHasErrorPropagation;

        public override void ExplicitVisit(TryCatchStatement node)
        {
            node.TryStatements?.Accept(this);

            var previousDepth = _catchDepth;
            var previousPropagation = _catchHasErrorPropagation;

            _catchDepth++;
            _catchHasErrorPropagation = false;
            node.CatchStatements?.Accept(this);

            if (!_catchHasErrorPropagation)
            {
                AddDiagnostic(
                    range: ScriptDomHelpers.GetCatchKeywordPairRange(node),
                    message: "CATCH block suppresses errors without THROW or RAISERROR. This creates silent failures that are difficult to debug. Consider rethrowing the error or logging to a persistent store.",
                    code: "avoid-catch-swallowing",
                    category: "Transactions",
                    fixable: false
                );
            }

            _catchDepth = previousDepth;
            _catchHasErrorPropagation = previousPropagation;
        }

        public override void ExplicitVisit(ThrowStatement node)
        {
            if (_catchDepth > 0)
            {
                _catchHasErrorPropagation = true;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(RaiseErrorStatement node)
        {
            if (_catchDepth > 0)
            {
                _catchHasErrorPropagation = true;
            }

            base.ExplicitVisit(node);
        }
    }
}
