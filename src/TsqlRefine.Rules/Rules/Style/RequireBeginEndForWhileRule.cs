using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

/// <summary>
/// Enforces BEGIN/END for every WHILE body to avoid accidental single-statement loops when code is edited.
/// </summary>
public sealed class RequireBeginEndForWhileRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-begin-end-for-while",
        Description: "Enforces BEGIN/END for every WHILE body to avoid accidental single-statement loops when code is edited.",
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

        var visitor = new RequireBeginEndForWhileVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
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
