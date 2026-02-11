using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects SET ROWCOUNT n statements which are deprecated and can cause unexpected behavior with triggers and nested statements.
/// </summary>
public sealed class AvoidSetRowcountRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-set-rowcount",
        Description: "Detects SET ROWCOUNT statements which are deprecated and can cause unexpected behavior with triggers and nested statements.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidSetRowcountVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidSetRowcountVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SetRowCountStatement node)
        {
            // Allow SET ROWCOUNT 0 (resets/disables the setting)
            if (node.NumberRows is IntegerLiteral literal && literal.Value == "0")
            {
                base.ExplicitVisit(node);
                return;
            }

            AddDiagnostic(
                fragment: node,
                message: "Avoid SET ROWCOUNT for limiting rows. It is deprecated and affects triggers and nested statements. Use TOP for SELECT or redesign for DML.",
                code: "avoid-set-rowcount",
                category: "Correctness",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
