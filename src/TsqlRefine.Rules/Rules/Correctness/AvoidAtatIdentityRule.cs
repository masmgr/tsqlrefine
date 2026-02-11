using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Disallows @@IDENTITY; it can return values from triggers - prefer SCOPE_IDENTITY() or OUTPUT.
/// </summary>
public sealed class AvoidAtatIdentityRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-atat-identity",
        Description: "Disallows @@IDENTITY; it can return values from triggers - prefer SCOPE_IDENTITY() or OUTPUT.",
        Category: "Correctness",
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

        var visitor = new AtatIdentityVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AtatIdentityVisitor : TSqlFragmentVisitor
    {
        private readonly List<Diagnostic> _diagnostics = new();

        public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

        public override void ExplicitVisit(GlobalVariableExpression node)
        {
            if (!string.Equals(node.Name, "@@IDENTITY", StringComparison.OrdinalIgnoreCase))
            {
                base.ExplicitVisit(node);
                return;
            }

            _diagnostics.Add(new Diagnostic(
                Range: ScriptDomHelpers.GetRange(node),
                Message: "Avoid @@IDENTITY; it can return values from triggers. Use SCOPE_IDENTITY() or OUTPUT clause instead.",
                Code: "avoid-atat-identity",
                Data: new DiagnosticData("avoid-atat-identity", "Correctness", false)
            ));

            base.ExplicitVisit(node);
        }
    }
}
