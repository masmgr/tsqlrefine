using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Debug;

public sealed class PrintStatementRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "print-statement",
        Description: "Prohibit PRINT statements; use RAISERROR for error messages and debugging",
        Category: "Debug",
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

        var visitor = new PrintStatementVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class PrintStatementVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(PrintStatement node)
        {
            AddDiagnostic(
                fragment: node,
                message: "PRINT statement found. Use RAISERROR for error messages and debugging instead.",
                code: "print-statement",
                category: "Debug",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
