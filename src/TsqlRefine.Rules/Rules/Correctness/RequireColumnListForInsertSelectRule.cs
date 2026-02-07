using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// INSERT SELECT statements must explicitly specify the column list to avoid errors when table schema changes
/// </summary>
public sealed class RequireColumnListForInsertSelectRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "require-column-list-for-insert-select",
        Description: "INSERT SELECT statements must explicitly specify the column list to avoid errors when table schema changes",
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

        var visitor = new RequireColumnListForInsertSelectVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class RequireColumnListForInsertSelectVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(InsertStatement node)
        {
            if (node.InsertSpecification?.InsertSource is SelectInsertSource)
            {
                var columns = node.InsertSpecification.Columns;
                if (columns is null || columns.Count == 0)
                {
                    AddDiagnostic(
                        fragment: node,
                        message: "INSERT SELECT statement without explicit column list. Specify column names to prevent errors when table schema changes.",
                        code: "require-column-list-for-insert-select",
                        category: "Correctness",
                        fixable: false
                    );
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
