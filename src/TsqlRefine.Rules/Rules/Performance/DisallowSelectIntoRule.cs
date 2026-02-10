using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Warns on SELECT ... INTO; it implicitly creates schema and can produce fragile, environment-dependent results.
/// </summary>
public sealed class DisallowSelectIntoRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "disallow-select-into",
        Description: "Warns on SELECT ... INTO; it implicitly creates schema and can produce fragile, environment-dependent results.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new DisallowSelectIntoVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DisallowSelectIntoVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SelectStatement node)
        {
            if (node.Into != null)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "Avoid SELECT...INTO; it implicitly creates schema and can produce environment-dependent results. Use CREATE TABLE + INSERT instead.",
                    code: "disallow-select-into",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
