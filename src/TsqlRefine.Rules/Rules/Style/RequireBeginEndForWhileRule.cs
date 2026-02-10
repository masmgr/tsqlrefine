using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Enforces BEGIN/END for every WHILE body to avoid accidental single-statement loops when code is edited.
/// </summary>
public sealed class RequireBeginEndForWhileRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "require-begin-end-for-while",
        Description: "Enforces BEGIN/END for every WHILE body to avoid accidental single-statement loops when code is edited.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new RequireBeginEndForWhileVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireBeginEndForWhileVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(WhileStatement node)
        {
            if (BeginEndHelpers.NeedsBeginEndBlock(node.Statement))
            {
                AddDiagnostic(
                    fragment: node,
                    message: "WHILE statement should use BEGIN/END block to avoid accidental single-statement loops.",
                    code: "require-begin-end-for-while",
                    category: "Style",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
