using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Enforces BEGIN/END for IF/ELSE blocks, while allowing a single control-flow statement (e.g., RETURN) without a block.
/// </summary>
public sealed class RequireBeginEndForIfWithControlflowExceptionRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-begin-end-for-if-with-controlflow-exception",
        Description: "Enforces BEGIN/END for IF/ELSE blocks, while allowing a single control-flow statement (e.g., RETURN) without a block.",
        Category: "Style",
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
            // Check THEN branch (allow control-flow statements without BEGIN/END)
            if (BeginEndHelpers.NeedsBeginEndBlock(node.ThenStatement, allowControlFlowWithoutBlock: true))
            {
                AddDiagnostic(
                    fragment: node,
                    message: "IF statement should use BEGIN/END block unless it contains a single control-flow statement (RETURN, BREAK, CONTINUE, THROW).",
                    code: "require-begin-end-for-if-with-controlflow-exception",
                    category: "Style",
                    fixable: false
                );
            }

            // Check ELSE branch (allow ELSE IF pattern and control-flow statements)
            if (node.ElseStatement is not null &&
                !BeginEndHelpers.IsElseIfPattern(node.ElseStatement) &&
                BeginEndHelpers.NeedsBeginEndBlock(node.ElseStatement, allowControlFlowWithoutBlock: true))
            {
                AddDiagnostic(
                    fragment: node.ElseStatement,
                    message: "ELSE statement should use BEGIN/END block unless it contains a single control-flow statement (RETURN, BREAK, CONTINUE, THROW).",
                    code: "require-begin-end-for-if-with-controlflow-exception",
                    category: "Style",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
