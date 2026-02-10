using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Detects nested ISNULL and recommends COALESCE; reduces nesting and aligns with standard SQL behavior.
/// </summary>
public sealed class PreferCoalesceOverNestedIsnullRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "prefer-coalesce-over-nested-isnull",
        Description: "Detects nested ISNULL and recommends COALESCE; reduces nesting and aligns with standard SQL behavior.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new PreferCoalesceOverNestedIsnullVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class PreferCoalesceOverNestedIsnullVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(FunctionCall node)
        {
            if (node.FunctionName.Value.Equals("ISNULL", StringComparison.OrdinalIgnoreCase))
            {
                // Check if any parameter is also an ISNULL call
                if (node.Parameters != null && node.Parameters.Count >= 2)
                {
                    foreach (var param in node.Parameters)
                    {
                        if (param is FunctionCall innerFunc &&
                            innerFunc.FunctionName.Value.Equals("ISNULL", StringComparison.OrdinalIgnoreCase))
                        {
                            AddDiagnostic(
                                fragment: node,
                                message: "Use COALESCE instead of nested ISNULL; it's clearer and supports more than two arguments.",
                                code: "prefer-coalesce-over-nested-isnull",
                                category: "Style",
                                fixable: false
                            );
                            break;
                        }
                    }
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
