using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Prohibit cursor usage; prefer set-based operations for better performance
/// </summary>
public sealed class DisallowCursorsRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "disallow-cursors",
        Description: "Prohibit cursor usage; prefer set-based operations for better performance",
        Category: "Performance",
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

        var visitor = new DisallowCursorsVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DisallowCursorsVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(DeclareCursorStatement node)
        {
            AddDiagnostic(
                fragment: node,
                message: "Cursor declaration found. Use set-based operations instead for better performance.",
                code: "disallow-cursors",
                category: "Performance",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
