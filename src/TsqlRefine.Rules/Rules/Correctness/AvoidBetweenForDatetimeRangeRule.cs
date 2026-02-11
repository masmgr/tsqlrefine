using System.Collections.Frozen;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects BETWEEN for datetime ranges. BETWEEN includes both endpoints, which can cause boundary issues with time components.
/// </summary>
public sealed class AvoidBetweenForDatetimeRangeRule : DiagnosticVisitorRuleBase
{
    private static readonly FrozenSet<string> s_datetimeFunctions = FrozenSet.ToFrozenSet(
        ["GETDATE", "SYSDATETIME", "GETUTCDATE", "SYSUTCDATETIME", "CURRENT_TIMESTAMP"],
        StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenSet<SqlDataTypeOption> s_datetimeTypes = FrozenSet.ToFrozenSet(
    [
        SqlDataTypeOption.DateTime,
        SqlDataTypeOption.DateTime2,
        SqlDataTypeOption.SmallDateTime,
        SqlDataTypeOption.DateTimeOffset,
        SqlDataTypeOption.Time,
    ]);

    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-between-for-datetime-range",
        Description: "Detects BETWEEN for datetime ranges. BETWEEN includes both endpoints, which can cause boundary issues with time components.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidBetweenForDatetimeRangeVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidBetweenForDatetimeRangeVisitor : PredicateAwareVisitorBase
    {
        public override void ExplicitVisit(BooleanTernaryExpression node)
        {
            if (IsInPredicate
                && node.TernaryExpressionType is BooleanTernaryExpressionType.Between
                && IsDatetimeRelated(node))
            {
                AddDiagnostic(
                    fragment: node,
                    message: "Avoid BETWEEN for datetime ranges. BETWEEN includes both endpoints, which can cause boundary issues with time components. Use >= and < pattern instead.",
                    code: "avoid-between-for-datetime-range",
                    category: "Correctness",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        private static bool IsDatetimeRelated(BooleanTernaryExpression node) =>
            HasTimeInName(node.FirstExpression)
            || HasTimeInName(node.SecondExpression)
            || HasTimeInName(node.ThirdExpression)
            || ContainsDatetimeFunction(node.FirstExpression)
            || ContainsDatetimeFunction(node.SecondExpression)
            || ContainsDatetimeFunction(node.ThirdExpression)
            || ContainsDatetimeCastOrConvert(node.FirstExpression)
            || ContainsDatetimeCastOrConvert(node.SecondExpression)
            || ContainsDatetimeCastOrConvert(node.ThirdExpression);

        private static bool HasTimeInName(ScalarExpression? expression)
        {
            var name = expression switch
            {
                ColumnReferenceExpression col => col.MultiPartIdentifier?.Identifiers.LastOrDefault()?.Value,
                VariableReference var => var.Name,
                _ => null
            };

            return name is not null && name.Contains("time", StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsDatetimeFunction(ScalarExpression? expression)
        {
            if (expression is null)
                return false;

            // Direct function call
            if (expression is FunctionCall func)
            {
                var funcName = func.FunctionName?.Value;
                if (funcName is not null && s_datetimeFunctions.Contains(funcName))
                    return true;

                // Check nested expressions in function parameters
                if (func.Parameters is not null)
                {
                    foreach (var param in func.Parameters)
                    {
                        if (ContainsDatetimeFunction(param))
                            return true;
                    }
                }
            }

            // CURRENT_TIMESTAMP is parsed as ParameterlessCall
            if (expression is ParameterlessCall parameterless
                && parameterless.ParameterlessCallType == ParameterlessCallType.CurrentTimestamp)
            {
                return true;
            }

            // Check binary expressions (e.g., GETDATE() - 7)
            if (expression is BinaryExpression binary)
            {
                return ContainsDatetimeFunction(binary.FirstExpression)
                    || ContainsDatetimeFunction(binary.SecondExpression);
            }

            // Check parenthesized expressions
            if (expression is ParenthesisExpression paren)
            {
                return ContainsDatetimeFunction(paren.Expression);
            }

            return false;
        }

        private static bool ContainsDatetimeCastOrConvert(ScalarExpression? expression)
        {
            if (expression is null)
                return false;

            if (expression is CastCall cast
                && cast.DataType is SqlDataTypeReference sqlType
                && s_datetimeTypes.Contains(sqlType.SqlDataTypeOption))
            {
                return true;
            }

            if (expression is ConvertCall convert
                && convert.DataType is SqlDataTypeReference convertType
                && s_datetimeTypes.Contains(convertType.SqlDataTypeOption))
            {
                return true;
            }

            return false;
        }
    }
}
