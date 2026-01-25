using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class AvoidExecDynamicSqlRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-exec-dynamic-sql",
        Description: "Detects EXEC with dynamic SQL (EXEC(...) pattern) which can be vulnerable to SQL injection",
        Category: "Security",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new AvoidExecDynamicSqlVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
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
