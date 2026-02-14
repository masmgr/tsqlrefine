using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Debug;

/// <summary>
/// Prohibit PRINT statements; use THROW or RAISERROR WITH NOWAIT for error messages and debugging
/// </summary>
public sealed class PrintStatementRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "print-statement",
        Description: "Prohibit PRINT statements; use THROW or RAISERROR WITH NOWAIT for error messages and debugging",
        Category: "Debug",
        DefaultSeverity: RuleSeverity.Information,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new PrintStatementVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class PrintStatementVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(PrintStatement node)
        {
            AddDiagnostic(
                range: ScriptDomHelpers.GetFirstTokenRange(node),
                message: "PRINT statement found. Use THROW or RAISERROR WITH NOWAIT for error messages. For debugging, use RAISERROR WITH NOWAIT to ensure immediate output.",
                code: "print-statement",
                category: "Debug",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
