using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Correctness;

public sealed class SetVariableRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/set-variable",
        Description: "Recommends using SELECT for variable assignment instead of SET for better performance and consistency.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Error,
        Fixable: false
    );

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Ast.Fragment is null)
        {
            yield break;
        }

        var visitor = new SetVariableVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class SetVariableVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SetVariableStatement node)
        {
            // SET @variable = value
            // This should be SELECT @variable = value instead
            AddDiagnostic(
                fragment: node,
                message: "Use SELECT for variable assignment instead of SET. SELECT is preferred for consistency and can be more performant when assigning multiple variables.",
                code: "semantic/set-variable",
                category: "Correctness",
                fixable: false
            );

            base.ExplicitVisit(node);
        }
    }
}
