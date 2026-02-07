using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Debug;

/// <summary>
/// Prohibit PRINT statements; use THROW or RAISERROR WITH NOWAIT for error messages and debugging
/// </summary>
public sealed class PrintStatementRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "print-statement",
        Description: "Prohibit PRINT statements; use THROW or RAISERROR WITH NOWAIT for error messages and debugging",
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
                message: "PRINT statement found. Use THROW or RAISERROR WITH NOWAIT for error messages. For debugging, use RAISERROR WITH NOWAIT to ensure immediate output.",
                code: "print-statement",
                category: "Debug",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
