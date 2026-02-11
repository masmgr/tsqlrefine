using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

/// <summary>
/// Recommends using SELECT for variable assignment instead of SET for consistency.
/// </summary>
public sealed class SetVariableRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "semantic-set-variable",
        Description: "Recommends using SELECT for variable assignment instead of SET for consistency.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new SetVariableVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class SetVariableVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SetVariableStatement node)
        {
            // SET @variable = value
            // This should be SELECT @variable = value instead
            AddDiagnostic(
                fragment: node,
                message: "Use SELECT for variable assignment instead of SET. SELECT is preferred for consistency and can be more performant when assigning multiple variables.",
                code: "semantic-set-variable",
                category: "Correctness",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
