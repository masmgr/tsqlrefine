using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules;

public sealed class RequireBeginEndForIfWithControlflowExceptionRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-begin-end-for-if-with-controlflow-exception",
        Description: "Enforces BEGIN/END for IF/ELSE blocks, while allowing a single control-flow statement (e.g., RETURN) without a block.",
        Category: "Control Flow Safety",
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

        var visitor = new RequireBeginEndForIfVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireBeginEndForIfVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(IfStatement node)
        {
            // Check THEN branch
            if (node.ThenStatement is not BeginEndBlockStatement && !IsControlFlowStatement(node.ThenStatement))
            {
                AddDiagnostic(
                    fragment: node,
                    message: "IF statement should use BEGIN/END block unless it contains a single control-flow statement (RETURN, BREAK, CONTINUE, THROW).",
                    code: "require-begin-end-for-if-with-controlflow-exception",
                    category: "Control Flow Safety",
                    fixable: false
                );
            }

            // Check ELSE branch
            if (node.ElseStatement is not null &&
                node.ElseStatement is not BeginEndBlockStatement &&
                node.ElseStatement is not IfStatement &&  // Allow ELSE IF pattern
                !IsControlFlowStatement(node.ElseStatement))
            {
                AddDiagnostic(
                    fragment: node.ElseStatement,
                    message: "ELSE statement should use BEGIN/END block unless it contains a single control-flow statement (RETURN, BREAK, CONTINUE, THROW).",
                    code: "require-begin-end-for-if-with-controlflow-exception",
                    category: "Control Flow Safety",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        private static bool IsControlFlowStatement(TSqlStatement statement)
        {
            return statement is ReturnStatement or
                   BreakStatement or
                   ContinueStatement or
                   ThrowStatement;
        }
    }
}
