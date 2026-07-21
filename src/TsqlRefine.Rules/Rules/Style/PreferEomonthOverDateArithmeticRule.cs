using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>Recommends EOMONTH over common DATEADD-based month-end calculations.</summary>
public sealed class PreferEomonthOverDateArithmeticRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "prefer-eomonth-over-date-arithmetic",
        Description: "Recommends EOMONTH over common DATEADD-based month-end calculations.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false);

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) => new Visitor();

    private sealed class Visitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(FunctionCall node)
        {
            if (IsDateAdd(node, "day", "dd", "d") &&
                IsInteger(node.Parameters.ElementAtOrDefault(1), -1) &&
                node.Parameters.ElementAtOrDefault(2) is FunctionCall inner &&
                IsDateAdd(inner, "month", "mm", "m") &&
                IsInteger(inner.Parameters.ElementAtOrDefault(1), 1) &&
                IsMonthStartExpression(inner.Parameters.ElementAtOrDefault(2)))
            {
                AddDiagnostic(node, "Use EOMONTH(expression) instead of DATEADD-based month-end calculation.");
            }

            base.ExplicitVisit(node);
        }

        private static bool IsDateAdd(FunctionCall call, params string[] dateParts) =>
            string.Equals(call.FunctionName?.Value, "DATEADD", StringComparison.OrdinalIgnoreCase) &&
            call.Parameters.Count >= 3 &&
            dateParts.Contains(GetDatePart(call.Parameters[0]), StringComparer.OrdinalIgnoreCase);

        private static string? GetDatePart(ScalarExpression expression) => expression switch
        {
            ColumnReferenceExpression column => column.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value,
            IdentifierLiteral identifier => identifier.Value,
            _ => null
        };

        private static bool IsMonthStartExpression(ScalarExpression? expression) =>
            expression is FunctionCall monthStart &&
            IsDateAdd(monthStart, "month", "mm", "m") &&
            monthStart.Parameters.ElementAtOrDefault(1) is FunctionCall difference &&
            IsDateDiff(difference, "month", "mm", "m") &&
            IsInteger(difference.Parameters.ElementAtOrDefault(1), 0) &&
            IsInteger(monthStart.Parameters.ElementAtOrDefault(2), 0);

        private static bool IsDateDiff(FunctionCall call, params string[] dateParts) =>
            string.Equals(call.FunctionName?.Value, "DATEDIFF", StringComparison.OrdinalIgnoreCase) &&
            call.Parameters.Count >= 3 &&
            dateParts.Contains(GetDatePart(call.Parameters[0]), StringComparer.OrdinalIgnoreCase);

        private static bool IsInteger(ScalarExpression? expression, int expected) =>
            expression is IntegerLiteral literal &&
            int.TryParse(literal.Value, out var value) &&
            value == expected ||
            expected < 0 && expression is UnaryExpression { UnaryExpressionType: UnaryExpressionType.Negative, Expression: IntegerLiteral negative } &&
            int.TryParse(negative.Value, out var absolute) &&
            -absolute == expected;
    }
}
