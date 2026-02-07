using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

/// <summary>
/// Detects LEFT JOIN operations where the WHERE clause filters the right-side table, effectively making it an INNER JOIN.
/// </summary>
public sealed class LeftJoinFilteredByWhereRule : IRule
{
    public RuleMetadata Metadata { get; } = new(
        RuleId: "semantic/left-join-filtered-by-where",
        Description: "Detects LEFT JOIN operations where the WHERE clause filters the right-side table, effectively making it an INNER JOIN.",
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

        var visitor = new LeftJoinFilteredByWhereVisitor();
        context.Ast.Fragment.Accept(visitor);

        foreach (var diagnostic in visitor.Diagnostics)
        {
            yield return diagnostic;
        }
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class LeftJoinFilteredByWhereVisitor : DiagnosticVisitorBase
    {
        public override void ExplicitVisit(SelectStatement node)
        {
            if (node.QueryExpression is not QuerySpecification querySpec)
            {
                base.ExplicitVisit(node);
                return;
            }

            if (querySpec.FromClause == null)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Collect LEFT JOIN right-side table aliases using helper
            var leftJoins = TableReferenceHelpers.CollectJoinsOfType(
                querySpec.FromClause.TableReferences,
                QualifiedJoinType.LeftOuter).ToList();

            // If no LEFT JOINs, nothing to check
            if (leftJoins.Count == 0)
            {
                base.ExplicitVisit(node);
                return;
            }

            // Build a map of right-side table names to their JOIN nodes
            var rightTableToJoin = new Dictionary<string, QualifiedJoin>(StringComparer.OrdinalIgnoreCase);
            foreach (var join in leftJoins)
            {
                var rightTableName = TableReferenceHelpers.GetAliasOrTableName(join.SecondTableReference);
                if (rightTableName != null)
                {
                    rightTableToJoin[rightTableName] = join;
                }
            }

            // Check WHERE clause for filters on right-side tables
            if (querySpec.WhereClause != null)
            {
                var filteredTables = FindFilteredRightSideTables(
                    querySpec.WhereClause.SearchCondition,
                    rightTableToJoin.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
                );

                foreach (var tableName in filteredTables)
                {
                    if (rightTableToJoin.TryGetValue(tableName, out var join))
                    {
                        AddDiagnostic(
                            fragment: join,
                            message: $"LEFT JOIN with table '{tableName}' is negated by WHERE clause filter. This effectively makes it an INNER JOIN. Consider using INNER JOIN instead, or move the filter to the ON clause, or use 'IS NOT NULL' to be explicit.",
                            code: "semantic/left-join-filtered-by-where",
                            category: "Correctness",
                            fixable: false
                        );
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        private static HashSet<string> FindFilteredRightSideTables(
            BooleanExpression? condition,
            HashSet<string> rightSideTables)
        {
            var filteredTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (condition == null)
            {
                return filteredTables;
            }

            FindFilteredTablesRecursive(condition, rightSideTables, filteredTables);

            return filteredTables;
        }

        private static void FindFilteredTablesRecursive(
            BooleanExpression condition,
            HashSet<string> rightSideTables,
            HashSet<string> filteredTables)
        {
            switch (condition)
            {
                case BooleanComparisonExpression comparison:
                    // Check if this is a filter on a right-side table
                    // But exclude IS NULL / IS NOT NULL checks (they preserve LEFT JOIN semantics)
                    CheckComparison(comparison, rightSideTables, filteredTables);
                    break;

                case BooleanIsNullExpression:
                    // IS NULL / IS NOT NULL on right-side tables is OK - doesn't negate LEFT JOIN
                    break;

                case BooleanBinaryExpression binary:
                    // Recursively check both sides of AND/OR
                    FindFilteredTablesRecursive(binary.FirstExpression, rightSideTables, filteredTables);
                    FindFilteredTablesRecursive(binary.SecondExpression, rightSideTables, filteredTables);
                    break;

                case BooleanParenthesisExpression paren:
                    FindFilteredTablesRecursive(paren.Expression, rightSideTables, filteredTables);
                    break;

                case InPredicate inPred:
                    // IN predicate on right-side table
                    if (inPred.Expression is ColumnReferenceExpression colRef)
                    {
                        var tableName = ColumnReferenceHelpers.GetTableQualifier(colRef);
                        if (tableName != null && rightSideTables.Contains(tableName))
                        {
                            filteredTables.Add(tableName);
                        }
                    }
                    break;
            }
        }

        private static void CheckComparison(
            BooleanComparisonExpression comparison,
            HashSet<string> rightSideTables,
            HashSet<string> filteredTables)
        {
            // Check if either side references a right-side table
            var firstTable = comparison.FirstExpression is ColumnReferenceExpression firstCol
                ? ColumnReferenceHelpers.GetTableQualifier(firstCol)
                : null;

            var secondTable = comparison.SecondExpression is ColumnReferenceExpression secondCol
                ? ColumnReferenceHelpers.GetTableQualifier(secondCol)
                : null;

            // If a right-side table is compared to a literal or another column, it's a filter
            if (firstTable != null && rightSideTables.Contains(firstTable))
            {
                filteredTables.Add(firstTable);
            }

            if (secondTable != null && rightSideTables.Contains(secondTable))
            {
                filteredTables.Add(secondTable);
            }
        }
    }
}
