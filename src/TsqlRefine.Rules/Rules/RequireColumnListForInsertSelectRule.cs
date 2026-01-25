using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules;

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

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(diagnostic);
        return Array.Empty<Fix>();
    }

    private sealed class RequireColumnListForInsertSelectVisitor : TSqlFragmentVisitor
    {
        private readonly List<Diagnostic> _diagnostics = new();
        public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

        public override void ExplicitVisit(InsertStatement node)
        {
            if (node.InsertSpecification?.InsertSource is SelectInsertSource)
            {
                var columns = node.InsertSpecification.Columns;
                if (columns is null || columns.Count == 0)
                {
                    _diagnostics.Add(new Diagnostic(
                        Range: GetRange(node),
                        Message: "INSERT SELECT statement without explicit column list. Specify column names to prevent errors when table schema changes.",
                        Code: "require-column-list-for-insert-select",
                        Data: new DiagnosticData("require-column-list-for-insert-select", "Correctness", false)
                    ));
                }
            }

            base.ExplicitVisit(node);
        }

        private static TsqlRefine.PluginSdk.Range GetRange(TSqlFragment fragment)
        {
            var start = new Position(fragment.StartLine - 1, fragment.StartColumn - 1);
            var end = start;

            // Try to get the end position from the last token
            if (fragment.ScriptTokenStream != null &&
                fragment.LastTokenIndex >= 0 &&
                fragment.LastTokenIndex < fragment.ScriptTokenStream.Count)
            {
                var lastToken = fragment.ScriptTokenStream[fragment.LastTokenIndex];
                var tokenText = lastToken.Text ?? string.Empty;
                end = new Position(
                    lastToken.Line - 1,
                    lastToken.Column - 1 + tokenText.Length
                );
            }

            return new TsqlRefine.PluginSdk.Range(start, end);
        }
    }
}
