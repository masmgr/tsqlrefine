using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Requires SET XACT_ABORT ON with explicit transactions to ensure runtime errors reliably abort and roll back work.
/// </summary>
public sealed class RequireXactAbortOnRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "set-xact-abort",
        Description: "Requires SET XACT_ABORT ON with explicit transactions to ensure runtime errors reliably abort and roll back work.",
        Category: "Transactions",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new RequireXactAbortOnVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireXactAbortOnVisitor : ProcedureScopeDiagnosticVisitorBase
    {
        private bool _hasXactAbortOn;

        public override void ExplicitVisit(PredicateSetStatement node)
        {
            if ((node.Options & SetOptions.XactAbort) == SetOptions.XactAbort)
            {
                _hasXactAbortOn = node.IsOn;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(BeginTransactionStatement node)
        {
            // Check if XACT_ABORT ON was set before this transaction
            if (!_hasXactAbortOn)
            {
                AddDiagnostic(
                    range: ScriptDomHelpers.GetFirstTokenRange(node),
                    message: "BEGIN TRANSACTION should be preceded by SET XACT_ABORT ON to ensure runtime errors reliably abort the transaction.",
                    code: "set-xact-abort",
                    category: "Transactions",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        protected override void VisitProcedureScope(Action visitChildren)
        {
            var parentValue = _hasXactAbortOn;
            _hasXactAbortOn = false;
            visitChildren();
            _hasXactAbortOn = parentValue;
        }
    }
}
