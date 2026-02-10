using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style;

public sealed class JoinKeywordRule : DiagnosticVisitorRuleBase
{
    private const string RuleId = "join-keyword";
    private const string Category = "Style";

    public override RuleMetadata Metadata { get; } = new(
        RuleId: RuleId,
        Description: "Detects comma-separated table lists in FROM clause (implicit joins) and suggests using explicit JOIN syntax for better readability",
        Category: Category,
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new CommaJoinVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    /// <summary>
    /// Visits QuerySpecification nodes to detect implicit comma joins in FROM clauses.
    /// When FromClause.TableReferences contains more than one entry, each extra entry
    /// represents a comma-separated table (implicit join).
    /// </summary>
    private sealed class CommaJoinVisitor : DiagnosticVisitorBase
    {
        private const string Message =
            "Avoid implicit joins using comma-separated table lists. Use explicit INNER JOIN, LEFT JOIN, or CROSS JOIN syntax for better readability and to prevent accidental Cartesian products.";

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.FromClause is { TableReferences.Count: > 1 } fromClause)
            {
                for (var i = 1; i < fromClause.TableReferences.Count; i++)
                {
                    AddDiagnostic(
                        fragment: fromClause.TableReferences[i],
                        message: Message,
                        code: RuleId,
                        category: Category,
                        fixable: false);
                }
            }

            base.ExplicitVisit(node);
        }
    }
}
