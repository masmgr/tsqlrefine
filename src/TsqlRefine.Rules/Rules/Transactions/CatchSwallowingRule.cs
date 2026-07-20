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
        public override void ExplicitVisit(TryCatchStatement node)
        {
            node.TryStatements?.Accept(this);

            var propagationVisitor = new EscapingErrorPropagationVisitor();
            node.CatchStatements?.Accept(propagationVisitor);
            if (!propagationVisitor.HasErrorPropagation)
            {
                AddDiagnostic(
                    range: ScriptDomHelpers.GetCatchKeywordPairRange(node),
                    message: "CATCH block suppresses errors without THROW or RAISERROR. This creates silent failures that are difficult to debug. Consider rethrowing the error or logging to a persistent store.",
                    code: "avoid-catch-swallowing",
                    category: "Transactions",
                    fixable: false
                );
            }

            node.CatchStatements?.Accept(this);
        }

        private sealed class EscapingErrorPropagationVisitor : TSqlFragmentVisitor
        {
            internal bool HasErrorPropagation { get; private set; }

            public override void ExplicitVisit(ThrowStatement node)
            {
                HasErrorPropagation = true;
            }

            public override void ExplicitVisit(RaiseErrorStatement node)
            {
                HasErrorPropagation = true;
            }

            public override void ExplicitVisit(TryCatchStatement node)
            {
                // Errors raised in the nested TRY are handled locally. Only propagation from
                // its CATCH can escape the current CATCH block.
                node.CatchStatements?.Accept(this);
            }
        }
    }
}
