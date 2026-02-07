using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Security;

public sealed class AvoidExecuteAsRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-execute-as",
        Description: "Detects EXECUTE AS usage for privilege escalation. EXECUTE AS can change the security context and may lead to unintended privilege escalation.",
        Category: "Security",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new AvoidExecuteAsVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidExecuteAsVisitor : DiagnosticVisitorBase
    {
        // Standalone EXECUTE AS USER = '...' / EXECUTE AS LOGIN = '...'
        public override void ExplicitVisit(ExecuteAsStatement node)
        {
            AddDiagnostic(
                fragment: node,
                message: $"Avoid EXECUTE AS {node.ExecuteContext.Kind}. Changing the security context can lead to unintended privilege escalation. Ensure this is intentional and properly paired with REVERT.",
                code: "avoid-execute-as",
                category: "Security",
                fixable: false
            );
            base.ExplicitVisit(node);
        }

        // EXECUTE AS clause in CREATE PROCEDURE
        public override void ExplicitVisit(ExecuteAsProcedureOption node)
        {
            CheckExecuteAsClause(node, node.ExecuteAs);
            base.ExplicitVisit(node);
        }

        // EXECUTE AS clause in CREATE FUNCTION
        public override void ExplicitVisit(ExecuteAsFunctionOption node)
        {
            CheckExecuteAsClause(node, node.ExecuteAs);
            base.ExplicitVisit(node);
        }

        // EXECUTE AS clause in CREATE TRIGGER
        public override void ExplicitVisit(ExecuteAsTriggerOption node)
        {
            CheckExecuteAsClause(node, node.ExecuteAsClause);
            base.ExplicitVisit(node);
        }

        private void CheckExecuteAsClause(TSqlFragment fragment, ExecuteAsClause? executeAsClause)
        {
            if (executeAsClause is null)
            {
                return;
            }

            // CALLER is the default and does not change the security context
            if (executeAsClause.ExecuteAsOption == ExecuteAsOption.Caller)
            {
                return;
            }

            var optionText = executeAsClause.ExecuteAsOption.ToString().ToUpperInvariant();
            if (executeAsClause.Literal is not null)
            {
                optionText = $"'{executeAsClause.Literal.Value}'";
            }

            AddDiagnostic(
                fragment: fragment,
                message: $"Avoid EXECUTE AS {optionText} clause. This changes the execution security context and may lead to unintended privilege escalation.",
                code: "avoid-execute-as",
                category: "Security",
                fixable: false
            );
        }
    }
}
