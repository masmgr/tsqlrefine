using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects UNION/UNION ALL where corresponding columns have obviously different literal types, which may cause implicit conversion or data truncation.
/// </summary>
public sealed class UnionTypeMismatchRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "union-type-mismatch",
        Description: "Detects UNION/UNION ALL where corresponding columns have obviously different literal types, which may cause implicit conversion or data truncation.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new UnionTypeMismatchVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class UnionTypeMismatchVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(BinaryQueryExpression node)
        {
            if (node.BinaryQueryExpressionType is BinaryQueryExpressionType.Union)
            {
                CheckUnionTypeMismatch(node);
            }

            base.ExplicitVisit(node);
        }

        private void CheckUnionTypeMismatch(BinaryQueryExpression node)
        {
            var leftColumns = GetSelectElements(node.FirstQueryExpression);
            var rightColumns = GetSelectElements(node.SecondQueryExpression);

            if (leftColumns is null || rightColumns is null)
            {
                return;
            }

            var count = Math.Min(leftColumns.Count, rightColumns.Count);

            for (var i = 0; i < count; i++)
            {
                var leftType = GetLiteralCategory(leftColumns[i]);
                var rightType = GetLiteralCategory(rightColumns[i]);

                if (leftType is not null && rightType is not null && leftType != rightType)
                {
                    AddDiagnostic(
                        fragment: rightColumns[i],
                        message: $"UNION column {i + 1} has mismatched types: left side is {leftType}, right side is {rightType}. This causes implicit conversion and may lead to data truncation or conversion errors.",
                        code: "union-type-mismatch",
                        category: "Correctness",
                        fixable: false
                    );
                }
            }
        }

        private static IList<SelectElement>? GetSelectElements(QueryExpression? queryExpression)
        {
            if (queryExpression is QuerySpecification querySpec)
            {
                return querySpec.SelectElements;
            }

            // For nested UNION, get the right-most query's columns
            if (queryExpression is BinaryQueryExpression bqe)
            {
                return GetSelectElements(bqe.SecondQueryExpression);
            }

            return null;
        }

        private static string? GetLiteralCategory(SelectElement element)
        {
            var expression = element is SelectScalarExpression scalar ? scalar.Expression : null;

            // Unwrap parenthesized expressions
            while (expression is ParenthesisExpression paren)
            {
                expression = paren.Expression;
            }

            // Unwrap CAST/CONVERT — the target type is what matters, not the source
            if (expression is CastCall castCall)
            {
                return GetDataTypeName(castCall.DataType);
            }

            if (expression is ConvertCall convertCall)
            {
                return GetDataTypeName(convertCall.DataType);
            }

            return expression switch
            {
                IntegerLiteral => "numeric",
                NumericLiteral => "numeric",
                RealLiteral => "numeric",
                MoneyLiteral => "numeric",
                StringLiteral => "string",
                NullLiteral => null,   // NULL is compatible with any type
                _ => null              // Can't determine type from column refs, expressions, etc.
            };
        }

        private static string? GetDataTypeName(DataTypeReference? dataType)
        {
            if (dataType is SqlDataTypeReference sqlType)
            {
                return sqlType.SqlDataTypeOption switch
                {
                    SqlDataTypeOption.Int or
                    SqlDataTypeOption.BigInt or
                    SqlDataTypeOption.SmallInt or
                    SqlDataTypeOption.TinyInt or
                    SqlDataTypeOption.Decimal or
                    SqlDataTypeOption.Numeric or
                    SqlDataTypeOption.Float or
                    SqlDataTypeOption.Real or
                    SqlDataTypeOption.Money or
                    SqlDataTypeOption.SmallMoney or
                    SqlDataTypeOption.Bit => "numeric",

                    SqlDataTypeOption.Char or
                    SqlDataTypeOption.VarChar or
                    SqlDataTypeOption.NChar or
                    SqlDataTypeOption.NVarChar or
                    SqlDataTypeOption.Text or
                    SqlDataTypeOption.NText => "string",

                    SqlDataTypeOption.Date or
                    SqlDataTypeOption.DateTime or
                    SqlDataTypeOption.DateTime2 or
                    SqlDataTypeOption.SmallDateTime or
                    SqlDataTypeOption.DateTimeOffset or
                    SqlDataTypeOption.Time => "datetime",

                    SqlDataTypeOption.Binary or
                    SqlDataTypeOption.VarBinary or
                    SqlDataTypeOption.Image => "binary",

                    SqlDataTypeOption.UniqueIdentifier => "uniqueidentifier",

                    _ => null
                };
            }

            return null;
        }
    }
}
