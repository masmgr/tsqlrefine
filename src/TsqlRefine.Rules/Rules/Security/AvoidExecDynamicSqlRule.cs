using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Security;

/// <summary>
/// Detects EXEC with dynamic SQL (EXEC(...) pattern) which can be vulnerable to SQL injection
/// </summary>
public sealed class AvoidExecDynamicSqlRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-exec-dynamic-sql",
        Description: "Detects EXEC with dynamic SQL (EXEC(...) pattern) which can be vulnerable to SQL injection",
        Category: "Security",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidExecDynamicSqlVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidExecDynamicSqlVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(ExecuteStatement node)
        {
            // Check if this is dynamic SQL execution (EXEC(@var) or EXEC('string'))
            if (node.ExecuteSpecification?.ExecutableEntity is ExecutableStringList executableStrings)
            {
                var isConstant = executableStrings.Strings.All(static expression => expression is StringLiteral);
                var message = isConstant
                    ? "Avoid executing constant SQL text with EXEC('...'). Prefer static SQL so dependencies remain visible to analysis and tooling."
                    : "Avoid dynamic SQL execution with EXEC(@variable). Consider using sp_executesql with parameters or static stored procedures to prevent SQL injection.";

                AddDiagnostic(
                    range: ScriptDomHelpers.GetFirstTokenRange(node),
                    message: message,
                    code: "avoid-exec-dynamic-sql",
                    category: "Security",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
