using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

public sealed class AvoidFloatForDecimalRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-float-for-decimal",
        Description: "Detects FLOAT/REAL data types which have binary rounding issues. Use DECIMAL/NUMERIC for exact precision.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new AvoidFloatVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
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
