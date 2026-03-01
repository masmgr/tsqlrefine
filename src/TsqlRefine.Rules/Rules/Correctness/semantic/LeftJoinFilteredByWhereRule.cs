using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Correctness.Semantic;

/// <summary>
/// Detects LEFT JOIN operations where the WHERE clause filters the right-side table, effectively making it an INNER JOIN.
/// When schema information is available, provides more specific diagnostic messages based on column nullability and FK relationships.
/// </summary>
public sealed class LeftJoinFilteredByWhereRule : DiagnosticVisitorRuleBase
{
    public override RuleMetadata Metadata { get; } = new(
        RuleId: "semantic-left-join-filtered-by-where",
        Description: "Detects LEFT JOIN operations where the WHERE clause filters the right-side table, effectively making it an INNER JOIN.",
        Category: "Correctness",
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new LeftJoinFilteredByWhereVisitor(context.Schema);

    public override IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    private sealed class LeftJoinFilteredByWhereVisitor(ISchemaProvider? schema) : DiagnosticVisitorBase
    {
        private const string DefaultMessageSuffix =
            "is negated by WHERE clause filter. This effectively makes it an INNER JOIN. " +
            "Consider using INNER JOIN instead, or move the filter to the ON clause, or use 'IS NOT NULL' to be explicit.";

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

            // Build alias map for schema resolution (when available)
            AliasMap? aliasMap = null;
            if (schema is not null && querySpec.FromClause.TableReferences is { Count: > 0 } tableRefs)
            {
                aliasMap = AliasMapBuilder.Build(tableRefs, schema);
            }

            // Check WHERE clause for filters on right-side tables
            if (querySpec.WhereClause != null)
            {
                var filteredTables = FindFilteredRightSideTablesWithColumns(
                    querySpec.WhereClause.SearchCondition,
                    rightTableToJoin.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
                );

                foreach (var (tableName, filteredColumnNames) in filteredTables)
                {
                    if (rightTableToJoin.TryGetValue(tableName, out var join))
                    {
                        var message = BuildDiagnosticMessage(tableName, filteredColumnNames, join, aliasMap);

                        AddDiagnostic(
                            fragment: join.SecondTableReference,
                            message: message,
                            code: "semantic-left-join-filtered-by-where",
                            category: "Correctness",
                            fixable: false
                        );
                    }
                }
            }

            base.ExplicitVisit(node);
        }

        private string BuildDiagnosticMessage(
            string tableName,
            List<string> filteredColumnNames,
            QualifiedJoin join,
            AliasMap? aliasMap)
        {
            var defaultMessage = $"LEFT JOIN with table '{tableName}' {DefaultMessageSuffix}";

            if (schema is null || aliasMap is null)
            {
                return defaultMessage;
            }

            // Try to resolve the right-side table
            var rightAlias = TableReferenceHelpers.GetAliasOrTableName(join.SecondTableReference);
            if (rightAlias is null || !aliasMap.TryResolve(rightAlias, out var resolvedRightTable) || resolvedRightTable is null)
            {
                return defaultMessage;
            }

            // Check nullability of filtered columns
            var allNotNull = true;
            var anyNotNull = false;
            var resolvedCount = 0;

            foreach (var colName in filteredColumnNames)
            {
                var resolved = schema.ResolveColumn(resolvedRightTable, colName);
                if (resolved is null)
                {
                    allNotNull = false;
                    continue;
                }

                resolvedCount++;
                if (!resolved.Column.IsNullable)
                {
                    anyNotNull = true;
                }
                else
                {
                    allNotNull = false;
                }
            }

            if (resolvedCount == 0)
            {
                return defaultMessage;
            }

            // Check if join follows a FK relationship
            var hasFkRelationship = HasForeignKeyRelationship(join, aliasMap, resolvedRightTable);

            if (allNotNull && filteredColumnNames.Count > 0)
            {
                var fkSuffix = hasFkRelationship
                    ? " The JOIN follows a foreign key relationship, confirming this conversion."
                    : "";
                return $"LEFT JOIN with table '{tableName}' is definitively converted to INNER JOIN by WHERE clause filter on NOT NULL column(s). Use INNER JOIN instead.{fkSuffix}";
            }

            if (anyNotNull)
            {
                return $"LEFT JOIN with table '{tableName}' is negated by WHERE clause filter (includes NOT NULL column(s)). This effectively makes it an INNER JOIN. Consider using INNER JOIN instead.";
            }

            // All filtered columns are nullable (or unresolvable)
            return defaultMessage;
        }

        private bool HasForeignKeyRelationship(
            QualifiedJoin join,
            AliasMap aliasMap,
            ResolvedTable rightTable)
        {
            if (join.SearchCondition is null)
            {
                return false;
            }

            // Extract equality pairs from ON clause
            var pairs = ExtractJoinEqualityPairs(join.SearchCondition);
            if (pairs.Count == 0)
            {
                return false;
            }

            // Resolve left-side table
            var leftAlias = TableReferenceHelpers.GetAliasOrTableName(join.FirstTableReference);
            if (leftAlias is null || !aliasMap.TryResolve(leftAlias, out var resolvedLeftTable) || resolvedLeftTable is null)
            {
                return false;
            }

            // Check FK from left to right
            var leftFks = schema!.GetForeignKeys(resolvedLeftTable);
            foreach (var fk in leftFks)
            {
                if (TablesAreEqual(fk.TargetTable, rightTable) && JoinColumnsMatchFk(pairs, fk, resolvedLeftTable, rightTable, aliasMap))
                {
                    return true;
                }
            }

            // Check FK from right to left
            var rightFks = schema.GetForeignKeys(rightTable);
            foreach (var fk in rightFks)
            {
                if (TablesAreEqual(fk.TargetTable, resolvedLeftTable) && JoinColumnsMatchFk(pairs, fk, rightTable, resolvedLeftTable, aliasMap))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<(string LeftQualifier, string LeftColumn, string RightQualifier, string RightColumn)> ExtractJoinEqualityPairs(
            BooleanExpression condition)
        {
            var results = new List<(string, string, string, string)>();
            CollectJoinEqualityPairs(condition, results);
            return results;
        }

        private static void CollectJoinEqualityPairs(
            BooleanExpression condition,
            List<(string, string, string, string)> results)
        {
            switch (condition)
            {
                case BooleanComparisonExpression { ComparisonType: BooleanComparisonType.Equals } comp
                    when comp.FirstExpression is ColumnReferenceExpression leftCol
                      && comp.SecondExpression is ColumnReferenceExpression rightCol:
                    var leftQualifier = ColumnReferenceHelpers.GetTableQualifier(leftCol);
                    var rightQualifier = ColumnReferenceHelpers.GetTableQualifier(rightCol);
                    if (leftQualifier is not null && rightQualifier is not null)
                    {
                        var leftColName = leftCol.MultiPartIdentifier!.Identifiers[^1].Value;
                        var rightColName = rightCol.MultiPartIdentifier!.Identifiers[^1].Value;
                        results.Add((leftQualifier, leftColName, rightQualifier, rightColName));
                    }

                    break;
                case BooleanBinaryExpression binary:
                    CollectJoinEqualityPairs(binary.FirstExpression, results);
                    CollectJoinEqualityPairs(binary.SecondExpression, results);
                    break;
                case BooleanParenthesisExpression paren:
                    CollectJoinEqualityPairs(paren.Expression, results);
                    break;
            }
        }

        private static bool JoinColumnsMatchFk(
            List<(string LeftQualifier, string LeftColumn, string RightQualifier, string RightColumn)> pairs,
            SchemaForeignKeyInfo fk,
            ResolvedTable sourceTable,
            ResolvedTable targetTable,
            AliasMap aliasMap)
        {
            if (fk.SourceColumns.Count != pairs.Count)
            {
                return false;
            }

            foreach (var (leftQ, leftC, rightQ, rightC) in pairs)
            {
                aliasMap.TryResolve(leftQ, out var leftResolved);
                aliasMap.TryResolve(rightQ, out var rightResolved);

                string? sourceCol = null, targetCol = null;
                if (leftResolved is not null && TablesAreEqual(leftResolved, sourceTable)
                    && rightResolved is not null && TablesAreEqual(rightResolved, targetTable))
                {
                    sourceCol = leftC;
                    targetCol = rightC;
                }
                else if (rightResolved is not null && TablesAreEqual(rightResolved, sourceTable)
                         && leftResolved is not null && TablesAreEqual(leftResolved, targetTable))
                {
                    sourceCol = rightC;
                    targetCol = leftC;
                }
                else
                {
                    return false;
                }

                var matched = false;
                for (var i = 0; i < fk.SourceColumns.Count; i++)
                {
                    if (string.Equals(fk.SourceColumns[i], sourceCol, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(fk.TargetColumns[i], targetCol, StringComparison.OrdinalIgnoreCase))
                    {
                        matched = true;
                        break;
                    }
                }

                if (!matched)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TablesAreEqual(ResolvedTable a, ResolvedTable b)
        {
            return string.Equals(a.SchemaName, b.SchemaName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(a.TableName, b.TableName, StringComparison.OrdinalIgnoreCase);
        }

        // --- Filtered table/column collection methods ---

        private static Dictionary<string, List<string>> FindFilteredRightSideTablesWithColumns(
            BooleanExpression? condition,
            HashSet<string> rightSideTables)
        {
            if (condition == null)
            {
                return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            }

            return FindFilteredTablesWithColumnsRecursive(condition, rightSideTables);
        }

        private static Dictionary<string, List<string>> FindFilteredTablesWithColumnsRecursive(
            BooleanExpression condition,
            HashSet<string> rightSideTables)
        {
            switch (condition)
            {
                case BooleanComparisonExpression comparison:
                    return GetFilteredTablesFromComparisonWithColumns(comparison, rightSideTables);

                case BooleanIsNullExpression:
                    // IS NULL / IS NOT NULL on right-side tables is OK - doesn't negate LEFT JOIN
                    return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                case BooleanBinaryExpression binary:
                    var first = FindFilteredTablesWithColumnsRecursive(binary.FirstExpression, rightSideTables);
                    var second = FindFilteredTablesWithColumnsRecursive(binary.SecondExpression, rightSideTables);
                    if (binary.BinaryExpressionType == BooleanBinaryExpressionType.Or)
                    {
                        return IntersectTableMaps(first, second);
                    }

                    return MergeTableMaps(first, second);

                case BooleanParenthesisExpression paren:
                    return FindFilteredTablesWithColumnsRecursive(paren.Expression, rightSideTables);

                case BooleanNotExpression notExpr:
                    // NOT on right-side filter still negates LEFT JOIN (NOT NULL = UNKNOWN)
                    return FindFilteredTablesWithColumnsRecursive(notExpr.Expression, rightSideTables);

                case InPredicate inPred:
                    return GetFilteredTablesFromExpressionWithColumn(inPred.Expression, rightSideTables);

                case LikePredicate likePred:
                    // LIKE on right-side table negates LEFT JOIN (NULLs fail LIKE)
                    return GetFilteredTablesFromExpressionWithColumn(likePred.FirstExpression, rightSideTables);

                case BooleanTernaryExpression ternary:
                    // BETWEEN on right-side table negates LEFT JOIN
                    return GetFilteredTablesFromExpressionWithColumn(ternary.FirstExpression, rightSideTables);

                default:
                    return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static Dictionary<string, List<string>> GetFilteredTablesFromComparisonWithColumns(
            BooleanComparisonExpression comparison,
            HashSet<string> rightSideTables)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            AddColumnFromExpression(comparison.FirstExpression, rightSideTables, result);
            AddColumnFromExpression(comparison.SecondExpression, rightSideTables, result);
            return result;
        }

        private static Dictionary<string, List<string>> GetFilteredTablesFromExpressionWithColumn(
            ScalarExpression expression,
            HashSet<string> rightSideTables)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            AddColumnFromExpression(expression, rightSideTables, result);
            return result;
        }

        private static void AddColumnFromExpression(
            ScalarExpression expression,
            HashSet<string> rightSideTables,
            Dictionary<string, List<string>> result)
        {
            if (expression is ColumnReferenceExpression colRef)
            {
                var tableName = ColumnReferenceHelpers.GetTableQualifier(colRef);
                if (tableName != null && rightSideTables.Contains(tableName))
                {
                    var columnName = colRef.MultiPartIdentifier?.Identifiers[^1].Value;
                    if (columnName != null)
                    {
                        if (!result.TryGetValue(tableName, out var columns))
                        {
                            columns = [];
                            result[tableName] = columns;
                        }

                        columns.Add(columnName);
                    }
                }
            }
        }

        private static Dictionary<string, List<string>> MergeTableMaps(
            Dictionary<string, List<string>> first,
            Dictionary<string, List<string>> second)
        {
            foreach (var (key, columns) in second)
            {
                if (first.TryGetValue(key, out var existing))
                {
                    existing.AddRange(columns);
                }
                else
                {
                    first[key] = columns;
                }
            }

            return first;
        }

        private static Dictionary<string, List<string>> IntersectTableMaps(
            Dictionary<string, List<string>> first,
            Dictionary<string, List<string>> second)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, columns) in first)
            {
                if (second.TryGetValue(key, out var secondColumns))
                {
                    var merged = new List<string>(columns);
                    merged.AddRange(secondColumns);
                    result[key] = merged;
                }
            }

            return result;
        }
    }
}
