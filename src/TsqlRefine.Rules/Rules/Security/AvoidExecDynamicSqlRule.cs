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
            if (node.ExecuteSpecification?.ExecutableEntity is ExecutableStringList)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "Avoid dynamic SQL execution with EXEC(@variable) or EXEC('string'). Consider using sp_executesql with parameters or static stored procedures to prevent SQL injection.",
                    code: "avoid-exec-dynamic-sql",
                    category: "Security",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
