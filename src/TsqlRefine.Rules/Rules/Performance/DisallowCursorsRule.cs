using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Prohibit cursor usage; prefer set-based operations for better performance
/// </summary>
public sealed class DisallowCursorsRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-cursors",
        Description: "Prohibit cursor usage; prefer set-based operations for better performance",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new DisallowCursorsVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DisallowCursorsVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(DeclareCursorStatement node)
        {
            AddDiagnostic(
                range: ScriptDomHelpers.GetFirstTokenRange(node),
                message: "Cursor declaration found. Use set-based operations instead for better performance.",
                code: "avoid-cursors",
                category: "Performance",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
