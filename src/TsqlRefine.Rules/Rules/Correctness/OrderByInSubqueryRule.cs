using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness;

/// <summary>
/// Detects ORDER BY in subqueries without TOP, OFFSET, FOR XML, or FOR JSON, which is wasteful as the optimizer may ignore it.
/// </summary>
public sealed class OrderByInSubqueryRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "order-by-in-subquery",
        Description: "Detects ORDER BY in subqueries without TOP, OFFSET, FOR XML, or FOR JSON, which is wasteful as the optimizer may ignore it.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new OrderByInSubqueryVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class OrderByInSubqueryVisitor : DiagnosticVisitorBase
    {
        private int _queryDepth;

        public override void ExplicitVisit(SelectStatement node)
        {
            _queryDepth++;

            // Check if this is a subquery (depth > 1) with ORDER BY
            if (_queryDepth > 1 && node.QueryExpression is QuerySpecification querySpec)
            {
                if (querySpec.OrderByClause != null)
                {
                    // Check for valid exceptions: TOP, OFFSET, FOR XML, FOR JSON
                    bool hasValidException = querySpec.TopRowFilter != null ||
                                           querySpec.OffsetClause != null ||
                                           (node.ComputeClauses != null && node.ComputeClauses.Count > 0);

                    // Check for FOR XML or FOR JSON
                    if (querySpec.ForClause != null)
                    {
                        hasValidException = true;
                    }

                    if (!hasValidException)
                    {
                        AddDiagnostic(
                            fragment: querySpec.OrderByClause,
                            message: "ORDER BY in subquery is invalid unless paired with TOP, OFFSET, FOR XML, or FOR JSON.",
                            code: "order-by-in-subquery",
                            category: "Correctness",
                            fixable: false
                        );
                    }
                }
            }

            base.ExplicitVisit(node);

            _queryDepth--;
        }

        public override void ExplicitVisit(QueryDerivedTable node)
        {
            _queryDepth++;
            base.ExplicitVisit(node);
            _queryDepth--;
        }

        public override void ExplicitVisit(ScalarSubquery node)
        {
            _queryDepth++;
            base.ExplicitVisit(node);
            _queryDepth--;
        }
    }
}
