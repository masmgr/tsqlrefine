using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Disallows @@IDENTITY; it can return values from triggers - prefer SCOPE_IDENTITY() or OUTPUT.
/// </summary>
public sealed class AvoidAtatIdentityRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-atat-identity",
        Description: "Disallows @@IDENTITY; it can return values from triggers - prefer SCOPE_IDENTITY() or OUTPUT.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) => new AtatIdentityVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AtatIdentityVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(GlobalVariableExpression node)
        {
            if (!string.Equals(node.Name, "@@IDENTITY", StringComparison.OrdinalIgnoreCase))
            {
                base.ExplicitVisit(node);
                return;
            }

            AddDiagnostic(
                fragment: node,
                message: "Avoid @@IDENTITY; it can return values from triggers. Use SCOPE_IDENTITY() or OUTPUT clause instead.",
                code: "avoid-atat-identity",
                category: "Correctness",
                fixable: false);

            base.ExplicitVisit(node);
        }
    }
}
