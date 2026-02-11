using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Style.Semantic;

/// <summary>
/// Requires column references in multi-table queries (with JOINs) to be qualified with table aliases for clarity.
/// </summary>
public sealed class MultiTableAliasRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "semantic-multi-table-alias",
        Description: "Requires column references in multi-table queries (with JOINs) to be qualified with table aliases for clarity.",
        Category: "Style",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new MultiTableAliasVisitor();

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class MultiTableAliasVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SelectStatement node)
        {
            if (node.QueryExpression is not QuerySpecification querySpec)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Check if the query has JOINs
            if (!HasJoins(querySpec.FromClause))
            {
                base.ExplicitVisit(node);
                return;
            }

            // Check all column references in the query
            var columnChecker = new ColumnReferenceChecker(this);

            // Check SELECT list
            if (querySpec.SelectElements != null)
            {
                foreach (var selectElement in querySpec.SelectElements)
                {
                    selectElement.Accept(columnChecker);
                }
            }

            // Check WHERE clause
            querySpec.WhereClause?.Accept(columnChecker);

            // Check GROUP BY clause
            querySpec.GroupByClause?.Accept(columnChecker);

            // Check HAVING clause
            querySpec.HavingClause?.Accept(columnChecker);

            // Check ORDER BY clause
            querySpec.OrderByClause?.Accept(columnChecker);

            // Check JOIN conditions
            if (querySpec.FromClause != null)
            {
                foreach (var tableRef in querySpec.FromClause.TableReferences)
                {
                    CheckJoinConditions(tableRef, columnChecker);
                }
            }

            base.ExplicitVisit(node);
        }

        private static bool HasJoins(FromClause? fromClause)
        {
            if (fromClause == null)
            {
                return false;
            }

            foreach (var tableRef in fromClause.TableReferences)
            {
                if (IsJoinReference(tableRef))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsJoinReference(TableReference tableRef)
        {
            return tableRef switch
            {
                JoinTableReference => true,
                _ => false
            };
        }

        private static void CheckJoinConditions(TableReference tableRef, ColumnReferenceChecker checker)
        {
            if (tableRef is QualifiedJoin qualifiedJoin)
            {
                qualifiedJoin.SearchCondition?.Accept(checker);
                CheckJoinConditions(qualifiedJoin.FirstTableReference, checker);
                CheckJoinConditions(qualifiedJoin.SecondTableReference, checker);
            }
            else if (tableRef is JoinTableReference join)
            {
                CheckJoinConditions(join.FirstTableReference, checker);
                CheckJoinConditions(join.SecondTableReference, checker);
            }
        }

        private sealed class ColumnReferenceChecker : TSqlFragmentVisitor
        {
            private readonly MultiTableAliasVisitor _parent;

            public ColumnReferenceChecker(MultiTableAliasVisitor parent)
            {
                _parent = parent;
            }

            public override void ExplicitVisit(ColumnReferenceExpression node)
            {
                // Check if the column reference is qualified
                if (node.MultiPartIdentifier?.Identifiers?.Count == 1)
                {
                    // Unqualified column reference (e.g., "Name" instead of "u.Name")
                    var columnName = node.MultiPartIdentifier.Identifiers[0].Value;
                    _parent.AddDiagnostic(
                        fragment: node,
                        message: $"Column reference '{columnName}' in multi-table query should be qualified with table alias (e.g., tablealias.{columnName}) to avoid ambiguity.",
                        code: "semantic-multi-table-alias",
                        category: "Style",
                        fixable: false
                    );
                }

                base.ExplicitVisit(node);
            }

            public override void ExplicitVisit(SelectStarExpression node)
            {
                // SELECT * is allowed even in multi-table queries
                // Don't report diagnostic for this
                base.ExplicitVisit(node);
            }

            // Don't descend into subqueries - they have their own scope
            public override void ExplicitVisit(SelectStatement node)
            {
                // Stop here - don't traverse into nested SELECT statements
            }
        }
    }
}
