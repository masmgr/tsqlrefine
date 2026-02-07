using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Warns on datetime CONVERT style numbers (magic numbers); encourages clearer, safer formatting patterns.
/// </summary>
public sealed class AvoidMagicConvertStyleForDatetimeRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-magic-convert-style-for-datetime",
        Description: "Warns on datetime CONVERT style numbers (magic numbers); encourages clearer, safer formatting patterns.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new AvoidMagicConvertStyleForDatetimeVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidMagicConvertStyleForDatetimeVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(ConvertCall node)
        {
            // Check if style parameter is provided and is a numeric literal
            if (node.Style is IntegerLiteral styleLiteral)
            {
                // Check if converting to/from datetime types
                var targetDataType = node.DataType as SqlDataTypeReference;
                var isTargetDateTime = targetDataType != null && IsDateTimeType(targetDataType.SqlDataTypeOption);

                // Check if the parameter expression involves datetime types
                // For example: CONVERT(VARCHAR, GETDATE(), 101) - converting from datetime
                var isSourceDateTime = InvolvesDateTimeExpression(node.Parameter);

                if (isTargetDateTime || isSourceDateTime)
                {
                    AddDiagnostic(
                        fragment: node,
                        message: $"Avoid magic number '{styleLiteral.Value}' for CONVERT style; use FORMAT() or explicit date parts for clearer intent.",
                        code: "avoid-magic-convert-style-for-datetime",
                        category: "Style",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }

        private static bool IsDateTimeType(SqlDataTypeOption dataType)
        {
            return dataType is SqlDataTypeOption.DateTime or
                   SqlDataTypeOption.DateTime2 or
                   SqlDataTypeOption.SmallDateTime or
                   SqlDataTypeOption.Date or
                   SqlDataTypeOption.Time or
                   SqlDataTypeOption.DateTimeOffset;
        }

        private static bool InvolvesDateTimeExpression(ScalarExpression expression)
        {
            // Check if expression is a datetime function call
            if (expression is FunctionCall funcCall)
            {
                var funcName = funcCall.FunctionName.Value;
                if (funcName.Equals("GETDATE", StringComparison.OrdinalIgnoreCase) ||
                    funcName.Equals("GETUTCDATE", StringComparison.OrdinalIgnoreCase) ||
                    funcName.Equals("SYSDATETIME", StringComparison.OrdinalIgnoreCase) ||
                    funcName.Equals("SYSUTCDATETIME", StringComparison.OrdinalIgnoreCase) ||
                    funcName.Equals("SYSDATETIMEOFFSET", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // For more complex cases, we'd need deeper analysis
            // This is a simple heuristic that covers common cases
            return false;
        }
    }
}
