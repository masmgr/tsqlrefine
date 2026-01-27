using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class ConditionalBeginEndRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "conditional-begin-end",
        Description: "Require BEGIN/END blocks in conditional statements for clarity and maintainability",
        Category: "Style",
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

        var visitor = new ConditionalBeginEndVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class ConditionalBeginEndVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(IfStatement node)
        {
            // Check THEN clause
            if (node.ThenStatement is not null && node.ThenStatement is not BeginEndBlockStatement)
            {
                AddDiagnostic(
                    fragment: node.ThenStatement,
                    message: "IF statement should use BEGIN/END block for clarity and maintainability.",
                    code: "conditional-begin-end",
                    category: "Style",
                    fixable: false
                );
            }

            // Check ELSE clause
            if (node.ElseStatement is not null && node.ElseStatement is not BeginEndBlockStatement && node.ElseStatement is not IfStatement)
            {
                AddDiagnostic(
                    fragment: node.ElseStatement,
                    message: "ELSE statement should use BEGIN/END block for clarity and maintainability.",
                    code: "conditional-begin-end",
                    category: "Style",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
