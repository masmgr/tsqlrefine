using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers;

namespace TsqlRefine.Rules.Rules.Safety;

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

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class DmlWithoutWhereVisitor : DiagnosticVisitorBase
    {

        public override void ExplicitVisit(UpdateStatement node)
        {
            if (node.UpdateSpecification?.WhereClause is null)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "UPDATE statement without WHERE clause can modify all rows. Add a WHERE clause to limit the scope.",
                    code: "dml-without-where",
                    category: "Safety",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(DeleteStatement node)
        {
            if (node.DeleteSpecification?.WhereClause is null)
            {
                AddDiagnostic(
                    fragment: node,
                    message: "DELETE statement without WHERE clause can delete all rows. Add a WHERE clause to limit the scope.",
                    code: "dml-without-where",
                    category: "Safety",
                    fixable: false
                );
            }

            base.ExplicitVisit(node);
        }
    }
}
