using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects duplicate output column names in SELECT queries.
/// This is a warning because some cases (e.g., intermediate queries) may be intentional.
/// </summary>
public sealed class DuplicateSelectColumnRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "duplicate-select-column",
        Description: "Detects duplicate output column names in SELECT queries; may cause ambiguous column references.",
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

        var visitor = new DuplicateSelectColumnVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DuplicateSelectColumnVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.SelectElements != null)
            {
                foreach (var (element, name) in SelectColumnHelpers.FindDuplicateColumns(node.SelectElements))
                {
                    AddDiagnostic(
                        fragment: (TSqlFragment)element,
                        message: $"Column '{name}' appears more than once in the SELECT list.",
                        code: "duplicate-select-column",
                        category: "Correctness",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
