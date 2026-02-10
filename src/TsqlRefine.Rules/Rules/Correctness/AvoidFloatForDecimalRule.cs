using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects FLOAT/REAL data types which have binary rounding issues. Use DECIMAL/NUMERIC for exact precision.
/// </summary>
public sealed class AvoidFloatForDecimalRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-float-for-decimal",
        Description: "Detects FLOAT/REAL data types which have binary rounding issues. Use DECIMAL/NUMERIC for exact precision.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidFloatVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidFloatVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SqlDataTypeReference node)
        {
            if (node.SqlDataTypeOption is SqlDataTypeOption.Float or SqlDataTypeOption.Real)
            {
                var typeName = node.SqlDataTypeOption == SqlDataTypeOption.Float ? "FLOAT" : "REAL";
                AddDiagnostic(
                    fragment: node,
                    message: $"Avoid '{typeName}' data type for values requiring exact precision. Floating-point types use binary representation which causes rounding errors in decimal arithmetic. Use DECIMAL(p,s) or NUMERIC(p,s) instead.",
                    code: "avoid-float-for-decimal",
                    category: "Correctness",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
