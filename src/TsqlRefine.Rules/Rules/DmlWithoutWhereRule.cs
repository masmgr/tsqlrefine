using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules;

public sealed class DmlWithoutWhereRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "dml-without-where",
        Description: "Detects UPDATE/DELETE statements without WHERE clause to prevent unintended mass data modifications.",
        Category: "Safety",
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

        var visitor = new DmlWithoutWhereVisitor();
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

    private sealed class DmlWithoutWhereVisitor : TSqlFragmentVisitor
    {
        private readonly List<Diagnostic> _diagnostics = new();
        public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;

        public override void ExplicitVisit(UpdateStatement node)
        {
            if (node.UpdateSpecification?.WhereClause is null)
            {
                _diagnostics.Add(new Diagnostic(
                    Range: GetRange(node),
                    Message: "UPDATE statement without WHERE clause can modify all rows. Add a WHERE clause to limit the scope.",
                    Code: "dml-without-where",
                    Data: new DiagnosticData("dml-without-where", "Safety", false)
                ));
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DeleteStatement node)
        {
            if (node.DeleteSpecification?.WhereClause is null)
            {
                _diagnostics.Add(new Diagnostic(
                    Range: GetRange(node),
                    Message: "DELETE statement without WHERE clause can delete all rows. Add a WHERE clause to limit the scope.",
                    Code: "dml-without-where",
                    Data: new DiagnosticData("dml-without-where", "Safety", false)
                ));
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
