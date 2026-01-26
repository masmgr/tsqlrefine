using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class AvoidMagicConvertStyleForDatetimeRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-magic-convert-style-for-datetime",
        Description: "Warns on datetime CONVERT style numbers (magic numbers); encourages clearer, safer formatting patterns.",
        Category: "Maintainability",
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
                var dataType = node.DataType as SqlDataTypeReference;
                if (dataType != null && IsDateTimeType(dataType.SqlDataTypeOption))
                {
                    AddDiagnostic(
                        fragment: node,
                        message: $"Avoid magic number '{styleLiteral.Value}' for CONVERT style; use FORMAT() or explicit date parts for clearer intent.",
                        code: "avoid-magic-convert-style-for-datetime",
                        category: "Maintainability",
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
    }
}
