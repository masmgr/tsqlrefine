using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

public sealed class ObjectPropertyRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "object-property",
        Description: "Prohibit OBJECTPROPERTY function; use OBJECTPROPERTYEX or sys catalog views instead",
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

        var visitor = new ObjectPropertyVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class ObjectPropertyVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(FunctionCall node)
        {
            // Check if function name is OBJECTPROPERTY
            if (node.FunctionName?.Value != null &&
                node.FunctionName.Value.Equals("OBJECTPROPERTY", StringComparison.OrdinalIgnoreCase))
            {
                AddDiagnostic(
                    fragment: node,
                    message: "OBJECTPROPERTY function found. Use OBJECTPROPERTYEX or sys catalog views instead for better performance and compatibility.",
                    code: "object-property",
                    category: "Performance",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
