using System.Globalization;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Transactions;

/// <summary>
/// Detects CATCH blocks that do not propagate the error via THROW, RAISERROR, or RETURN with error code.
/// </summary>
public sealed class RequireThrowOrRaiserrorInCatchRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "require-throw-or-raiserror-in-catch",
        Description: "Detects CATCH blocks that do not propagate the error via THROW, RAISERROR, or RETURN with error code.",
        Category: "Transactions",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new RequireThrowOrRaiserrorInCatchVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireThrowOrRaiserrorInCatchVisitor : DiagnosticVisitorBase
    {
        private int _catchDepth;
        private bool _catchHasErrorPropagation;

        public override void ExplicitVisit(TryCatchStatement node)
        {
            // Visit TRY block normally (don't track propagation in TRY)
            node.TryStatements?.Accept(this);

            // Track CATCH block for error propagation
            var previousDepth = _catchDepth;
            var previousPropagation = _catchHasErrorPropagation;

            _catchDepth++;
            _catchHasErrorPropagation = false;

            node.CatchStatements?.Accept(this);

            if (!_catchHasErrorPropagation)
            {
                AddDiagnostic(
                    range: ScriptDomHelpers.GetLeadingKeywordPairRange(node),
                    message: "CATCH block does not propagate the error. Add THROW, RAISERROR, or RETURN with error code to prevent silent failures.",
                    code: "require-throw-or-raiserror-in-catch",
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

        public override void ExplicitVisit(ReturnStatement node)
        {
            if (_catchDepth > 0 && IsErrorReturn(node.Expression))
            {
                _catchHasErrorPropagation = true;
            }

            base.ExplicitVisit(node);
        }

        private static bool IsErrorReturn(ScalarExpression? expression)
        {
            if (expression is null)
                return false;

            if (expression is IntegerLiteral integer)
                return integer.Value != "0";

            if (expression is NumericLiteral numeric
                && decimal.TryParse(
                    numeric.Value,
                    NumberStyles.Number,
                    CultureInfo.InvariantCulture,
                    out var value))
            {
                return value != 0m;
            }

            // Non-literal RETURN values (variables/expressions) are treated as possible error propagation.
            return true;
        }
    }
}
