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
            if (condition == null)
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return FindFilteredTablesRecursive(condition, rightSideTables);
        }

        private static HashSet<string> FindFilteredTablesRecursive(
            BooleanExpression condition,
            HashSet<string> rightSideTables)
        {
            switch (condition)
            {
                case BooleanComparisonExpression comparison:
                    // Check if this is a filter on a right-side table
                    return GetFilteredTablesFromComparison(comparison, rightSideTables);

                case BooleanIsNullExpression:
                    // IS NULL / IS NOT NULL on right-side tables is OK - doesn't negate LEFT JOIN
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                case BooleanBinaryExpression binary:
                    // For OR, table filters are only guaranteed if both branches reject NULL-extended rows.
                    var first = FindFilteredTablesRecursive(binary.FirstExpression, rightSideTables);
                    var second = FindFilteredTablesRecursive(binary.SecondExpression, rightSideTables);
                    if (binary.BinaryExpressionType == BooleanBinaryExpressionType.Or)
                    {
                        first.IntersectWith(second);
                        return first;
                    }

                    first.UnionWith(second);
                    return first;

                case BooleanParenthesisExpression paren:
                    return FindFilteredTablesRecursive(paren.Expression, rightSideTables);

                case BooleanNotExpression notExpr:
                    // NOT on right-side filter still negates LEFT JOIN (NOT NULL = UNKNOWN)
                    return FindFilteredTablesRecursive(notExpr.Expression, rightSideTables);

                case InPredicate inPred:
                    // IN predicate on right-side table
                    return GetFilteredTablesFromColumnReference(inPred.Expression, rightSideTables);

                case LikePredicate likePred:
                    // LIKE on right-side table negates LEFT JOIN (NULLs fail LIKE)
                    return GetFilteredTablesFromColumnReference(likePred.FirstExpression, rightSideTables);

                case BooleanTernaryExpression ternary:
                    // BETWEEN on right-side table negates LEFT JOIN
                    return GetFilteredTablesFromColumnReference(ternary.FirstExpression, rightSideTables);

                default:
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static HashSet<string> GetFilteredTablesFromColumnReference(
            ScalarExpression expression,
            HashSet<string> rightSideTables)
        {
            var filteredTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (expression is ColumnReferenceExpression colRef)
            {
                var tableName = ColumnReferenceHelpers.GetTableQualifier(colRef);
                if (tableName != null && rightSideTables.Contains(tableName))
                {
                    filteredTables.Add(tableName);
                }
            }

            return filteredTables;
        }

        private static HashSet<string> GetFilteredTablesFromComparison(
            BooleanComparisonExpression comparison,
            HashSet<string> rightSideTables)
        {
            var filteredTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

            return filteredTables;
        }
    }
}
