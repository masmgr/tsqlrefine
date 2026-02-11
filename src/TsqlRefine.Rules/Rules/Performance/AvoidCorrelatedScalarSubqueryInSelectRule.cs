using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Performance;

/// <summary>
/// Detects correlated scalar subqueries in SELECT list which execute once per row and cause severe performance degradation.
/// </summary>
public sealed class AvoidCorrelatedScalarSubqueryInSelectRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "avoid-correlated-scalar-subquery-in-select",
        Description: "Detects correlated scalar subqueries in SELECT list which execute once per row and cause severe performance degradation.",
        Category: "Performance",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new AvoidCorrelatedScalarSubqueryInSelectVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class AvoidCorrelatedScalarSubqueryInSelectVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.FromClause is not null)
            {
                var outerAliases = TableReferenceHelpers.CollectTableAliases(node.FromClause.TableReferences);

                if (outerAliases.Count > 0)
                {
                    foreach (var selectElement in node.SelectElements)
                    {
                        if (selectElement is SelectScalarExpression scalarExpr)
                        {
                            var collector = new ScalarSubqueryCollector();
                            scalarExpr.Accept(collector);

                            foreach (var subquery in collector.Found)
                            {
                                if (IsCorrelated(subquery, outerAliases))
                                {
                                    AddDiagnostic(
                                        fragment: subquery,
                                        message: "Avoid correlated scalar subqueries in the SELECT list. This subquery executes once per outer row, causing performance degradation. Consider using JOIN, CROSS APPLY, or a window function instead.",
                                        code: "avoid-correlated-scalar-subquery-in-select",
                                        category: "Performance",
                                        fixable: false
                                    );
                                }
                            }
                        }
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        private static bool IsCorrelated(ScalarSubquery subquery, HashSet<string> outerAliases)
        {
            if (subquery.QueryExpression is not QuerySpecification querySpec)
            {
                return false;
            }

            // Collect the subquery's own local aliases
            var localAliases = querySpec.FromClause is not null
                ? TableReferenceHelpers.CollectTableAliases(querySpec.FromClause.TableReferences)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Collect qualifiers from the subquery body (ScopeBlockingVisitor-based, stops at nested subqueries)
            var referencedQualifiers = ColumnReferenceHelpers.CollectTableQualifiers(querySpec);

            foreach (var qualifier in referencedQualifiers)
            {
                if (outerAliases.Contains(qualifier) && !localAliases.Contains(qualifier))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Collects ScalarSubquery nodes from an expression without descending into their children.
    /// </summary>
    private sealed class ScalarSubqueryCollector : TSqlFragmentVisitor
    {
        public List<ScalarSubquery> Found { get; } = [];

        public override void ExplicitVisit(ScalarSubquery node)
        {
            Found.Add(node);
            // Do NOT call base — we don't want to descend into the subquery's internals
        }

        public override void ExplicitVisit(QueryDerivedTable node)
        {
            // Stop at query boundaries
        }

        public override void ExplicitVisit(SelectStatement node)
        {
            // Stop at query boundaries
        }
    }
}
