using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Safety;

/// <summary>
/// Avoid using MERGE statement due to known bugs (see KB 3180087, KB 4519788)
/// </summary>
public sealed class AvoidMergeRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-merge",
        Description: "Avoid using MERGE statement due to known bugs (see KB 3180087, KB 4519788)",
        Category: "Safety",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidMergeVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidMergeVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(MergeStatement node)
        {
            AddDiagnostic(
                range: ScriptDomHelpers.GetFirstTokenRange(node),
                message: "Avoid MERGE statement due to known bugs (see KB 3180087, KB 4519788). MERGE can cause data corruption, race conditions, and non-deterministic behavior. Use explicit INSERT, UPDATE, DELETE statements with proper transaction handling instead.",
                code: "avoid-merge",
                category: "Safety",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
