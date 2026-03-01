using System.Globalization;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Schema;

namespace TsqlRefine.Rules.Rules.Schema;

/// <summary>
/// Detects JOINs where the column combination deviates from the dominant pattern
/// observed in the relation profile, including rare/very-rare patterns, unseen patterns,
/// and unknown table pairs.
/// </summary>
public sealed class JoinColumnDeviationRule : SchemaAndDeviationAwareVisitorRuleBase
{
    private const string RuleCode = "join-column-deviation";
    private const string Category = "Schema";

    public override RuleMetadata Metadata { get; } = new(
        RuleId: RuleCode,
        Description: "Detects JOINs where the column combination deviates from the dominant pattern observed in the relation profile.",
        Category: Category,
        DefaultSeverity: RuleSeverity.Warning,
        Fixable: false
    );

    protected override DiagnosticVisitorBase CreateVisitor(RuleContext context) =>
        new JoinColumnDeviationVisitor(context.Schema!, context.RelationDeviations!);

    private sealed class JoinColumnDeviationVisitor(
        ISchemaProvider schema,
        IRelationDeviationProvider deviations) : DiagnosticVisitorBase
    {
        private AliasMap? _currentAliasMap;

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.FromClause?.TableReferences is { Count: > 0 } tableRefs)
            {
                var previousMap = _currentAliasMap;
                _currentAliasMap = AliasMapBuilder.Build(tableRefs, schema);

                node.FromClause.Accept(this);

                _currentAliasMap = previousMap;
                return;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(QualifiedJoin node)
        {
            if (_currentAliasMap is not null && node.SearchCondition is not null)
            {
                CheckJoinColumnDeviation(node);
            }

            base.ExplicitVisit(node);
        }

        private void CheckJoinColumnDeviation(QualifiedJoin node)
        {
            // Only process INNER/LEFT/RIGHT/FULL JOINs
            var joinType = GetJoinTypeName(node.QualifiedJoinType);
            if (joinType is null)
            {
                return;
            }

            // Collect tables from each side of the JOIN
            var leftTableRef = ResolveFirstNamedTable(node.FirstTableReference);
            var rightTableRef = ResolveFirstNamedTable(node.SecondTableReference);
            if (leftTableRef is null || rightTableRef is null)
            {
                return;
            }

            var leftTable = leftTableRef;
            var rightTable = rightTableRef;

            // Extract equality column pairs from ON clause (AND-connected only, skip OR)
            var rawPairs = ExtractEqualityPairs(node.SearchCondition);
            if (rawPairs.Count == 0)
            {
                return;
            }

            // Orient column pairs so Left = FirstTableReference side, Right = SecondTableReference side
            var columnPairs = OrientColumnPairs(rawPairs, leftTable, rightTable);
            if (columnPairs.Count == 0)
            {
                return;
            }

            // Canonicalize: ensure lexicographic table order
            var leftKey = $"{leftTable.SchemaName}.{leftTable.TableName}";
            var rightKey = $"{rightTable.SchemaName}.{rightTable.TableName}";

            string canonLeftSchema, canonLeftTable, canonRightSchema, canonRightTable;
            string canonJoinType;
            List<(string Left, string Right)> canonPairs;

            if (string.Compare(leftKey, rightKey, StringComparison.OrdinalIgnoreCase) <= 0)
            {
                canonLeftSchema = leftTable.SchemaName;
                canonLeftTable = leftTable.TableName;
                canonRightSchema = rightTable.SchemaName;
                canonRightTable = rightTable.TableName;
                canonJoinType = joinType;
                canonPairs = columnPairs;
            }
            else
            {
                // Swap tables and column pair sides
                canonLeftSchema = rightTable.SchemaName;
                canonLeftTable = rightTable.TableName;
                canonRightSchema = leftTable.SchemaName;
                canonRightTable = leftTable.TableName;
                canonJoinType = SwapJoinDirection(joinType);
                canonPairs = columnPairs.Select(p => (p.RightColumn, p.LeftColumn)).ToList();
            }

            // Build sorted column pair descriptions for comparison
            var sortedDescriptions = canonPairs
                .Select(p => $"{p.Left}={p.Right}")
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Get table pair summary from deviation data
            var summary = deviations.GetTablePairSummary(
                canonLeftSchema, canonLeftTable, canonRightSchema, canonRightTable);

            if (summary is null)
            {
                // Unknown table pair
                if (deviations.HasData)
                {
                    var msg = $"JOIN between {canonLeftSchema}.{canonLeftTable} and " +
                        $"{canonRightSchema}.{canonRightTable} was not found in the relation profile.";
                    AddDiagnostic(
                        fragment: node.SecondTableReference,
                        message: msg,
                        code: RuleCode,
                        category: Category,
                        fixable: false);
                }

                return;
            }

            // Find matching deviation by JoinType + sorted ColumnPairDescriptions
            var matches = FindMatchingDeviations(summary, canonJoinType, sortedDescriptions);

            if (matches.Count == 0)
            {
                // Unseen pattern
                var dominantInfo = GetDominantInfo(summary);
                var pairDesc = string.Join(", ", sortedDescriptions);
                var msg = $"JOIN between {canonLeftSchema}.{canonLeftTable} and " +
                    $"{canonRightSchema}.{canonRightTable} on [{pairDesc}] was not observed " +
                    $"in the relation profile ({summary.Total} total occurrences for this table pair).{dominantInfo}";
                AddDiagnostic(
                    fragment: node.SearchCondition!,
                    message: msg,
                    code: RuleCode,
                    category: Category,
                    fixable: false);
                return;
            }

            if (matches.Count > 1)
            {
                // Ambiguous match — skip to avoid false positives
                return;
            }

            var deviation = matches[0];

            // Dominant, Common, InsufficientData: no warning
            if (deviation.Level is RelationDeviationLevel.Rare
                or RelationDeviationLevel.VeryRare
                or RelationDeviationLevel.Structural)
            {
                var levelName = deviation.Level.ToString().ToLowerInvariant();
                var pct = (deviation.Ratio * 100).ToString("F0", CultureInfo.InvariantCulture);
                var dominantInfo = GetDominantInfo(summary);
                var pairDesc = string.Join(", ", sortedDescriptions);
                var msg = $"JOIN between {canonLeftSchema}.{canonLeftTable} and " +
                    $"{canonRightSchema}.{canonRightTable} on [{pairDesc}] is {levelName} " +
                    $"({deviation.OccurrenceCount}/{summary.Total} occurrences, {pct}%).{dominantInfo}";
                AddDiagnostic(
                    fragment: node.SearchCondition!,
                    message: msg,
                    code: RuleCode,
                    category: Category,
                    fixable: false);
            }
        }

        private static List<RelationPatternDeviation> FindMatchingDeviations(
            RelationTablePairSummary summary,
            string joinType,
            List<string> sortedDescriptions)
        {
            var matches = new List<RelationPatternDeviation>();

            foreach (var deviation in summary.Deviations)
            {
                if (!string.Equals(deviation.JoinType, joinType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (deviation.ColumnPairDescriptions.Count != sortedDescriptions.Count)
                {
                    continue;
                }

                var sorted = deviation.ColumnPairDescriptions
                    .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var match = true;
                for (var i = 0; i < sorted.Count; i++)
                {
                    if (!string.Equals(sorted[i], sortedDescriptions[i], StringComparison.OrdinalIgnoreCase))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    matches.Add(deviation);
                }
            }

            return matches;
        }

        private static string GetDominantInfo(RelationTablePairSummary summary)
        {
            var dominant = summary.Deviations
                .FirstOrDefault(d => d.Level == RelationDeviationLevel.Dominant);

            if (dominant is null)
            {
                return string.Empty;
            }

            var sortedPairs = dominant.ColumnPairDescriptions
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var pct = (dominant.Ratio * 100).ToString("F0", CultureInfo.InvariantCulture);
            return $" The dominant pattern uses [{string.Join(", ", sortedPairs)}] ({pct}%).";
        }

        private static string? GetJoinTypeName(QualifiedJoinType joinType) =>
            joinType switch
            {
                QualifiedJoinType.Inner => "INNER",
                QualifiedJoinType.LeftOuter => "LEFT",
                QualifiedJoinType.RightOuter => "RIGHT",
                QualifiedJoinType.FullOuter => "FULL",
                _ => null,
            };

        private static string SwapJoinDirection(string joinType) =>
            joinType.ToUpperInvariant() switch
            {
                "LEFT" => "RIGHT",
                "RIGHT" => "LEFT",
                _ => joinType,
            };

        private static List<(ColumnReferenceExpression Left, ColumnReferenceExpression Right, BooleanComparisonExpression Node)>
            ExtractEqualityPairs(
            BooleanExpression? condition)
        {
            var results = new List<(ColumnReferenceExpression, ColumnReferenceExpression, BooleanComparisonExpression)>();
            CollectEqualityPairs(condition, results);
            return results;
        }

        private static void CollectEqualityPairs(
            BooleanExpression? condition,
            List<(ColumnReferenceExpression, ColumnReferenceExpression, BooleanComparisonExpression)> results)
        {
            switch (condition)
            {
                case BooleanComparisonExpression { ComparisonType: BooleanComparisonType.Equals } comparison
                    when comparison.FirstExpression is ColumnReferenceExpression leftCol
                      && comparison.SecondExpression is ColumnReferenceExpression rightCol:
                    results.Add((leftCol, rightCol, comparison));
                    break;

                case BooleanBinaryExpression { BinaryExpressionType: BooleanBinaryExpressionType.And } binary:
                    CollectEqualityPairs(binary.FirstExpression, results);
                    CollectEqualityPairs(binary.SecondExpression, results);
                    break;

                case BooleanParenthesisExpression paren:
                    CollectEqualityPairs(paren.Expression, results);
                    break;
            }
        }

        private (ResolvedTable Table, string ColumnName)? ResolveColumnToTable(ColumnReferenceExpression colRef)
        {
            if (_currentAliasMap is null || colRef.ColumnType == ColumnType.Wildcard)
            {
                return null;
            }

            var identifiers = colRef.MultiPartIdentifier?.Identifiers;
            if (identifiers is null or { Count: 0 })
            {
                return null;
            }

            var columnName = identifiers[identifiers.Count - 1].Value;

            if (identifiers.Count >= 2)
            {
                foreach (var key in BuildQualifierLookupKeys(identifiers))
                {
                    if (_currentAliasMap.TryResolve(key, out var resolved))
                    {
                        return resolved is null ? null : (resolved, columnName);
                    }
                }

                return null;
            }

            foreach (var table in _currentAliasMap.AllTables)
            {
                if (schema.ResolveColumn(table, columnName) is not null)
                {
                    return (table, columnName);
                }
            }

            return null;
        }

        private static IEnumerable<string> BuildQualifierLookupKeys(IList<Identifier> identifiers)
        {
            var qualifierCount = identifiers.Count - 1;
            if (qualifierCount <= 0)
            {
                yield break;
            }

            var parts = new string[qualifierCount];
            for (var i = 0; i < qualifierCount; i++)
            {
                parts[i] = identifiers[i].Value;
            }

            if (parts.Length == 1)
            {
                yield return parts[0];
                yield break;
            }

            yield return string.Join(".", parts);

            if (parts.Length >= 2)
            {
                yield return $"{parts[^2]}.{parts[^1]}";
            }

            yield return parts[^1];
        }

        /// <summary>
        /// Orients column pairs so Left belongs to leftTable and Right to rightTable,
        /// regardless of the order they appear in the ON clause.
        /// </summary>
        private List<(string LeftColumn, string RightColumn)> OrientColumnPairs(
            List<(ColumnReferenceExpression Left, ColumnReferenceExpression Right,
                BooleanComparisonExpression Node)> rawPairs,
            ResolvedTable leftTable,
            ResolvedTable rightTable)
        {
            var result = new List<(string, string)>();
            foreach (var (col1, col2, _) in rawPairs)
            {
                var resolved1 = ResolveColumnToTable(col1);
                var resolved2 = ResolveColumnToTable(col2);
                if (resolved1 is null || resolved2 is null)
                {
                    continue;
                }

                if (TablesAreEqual(resolved1.Value.Table, leftTable) &&
                    TablesAreEqual(resolved2.Value.Table, rightTable))
                {
                    result.Add((resolved1.Value.ColumnName, resolved2.Value.ColumnName));
                }
                else if (TablesAreEqual(resolved1.Value.Table, rightTable) &&
                         TablesAreEqual(resolved2.Value.Table, leftTable))
                {
                    result.Add((resolved2.Value.ColumnName, resolved1.Value.ColumnName));
                }
            }

            return result;
        }

        private ResolvedTable? ResolveFirstNamedTable(TableReference tableRef)
        {
            if (tableRef is NamedTableReference named)
            {
                var alias = named.Alias?.Value
                    ?? named.SchemaObject.BaseIdentifier?.Value;
                if (alias is not null && _currentAliasMap?.TryResolve(alias, out var resolved) == true)
                {
                    return resolved;
                }
            }
            else if (tableRef is JoinTableReference join)
            {
                // For nested JOINs, try the rightmost (second) table first
                return ResolveFirstNamedTable(join.SecondTableReference)
                    ?? ResolveFirstNamedTable(join.FirstTableReference);
            }

            return null;
        }

        private static bool TablesAreEqual(ResolvedTable a, ResolvedTable b) =>
            string.Equals(a.SchemaName, b.SchemaName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.TableName, b.TableName, StringComparison.OrdinalIgnoreCase);
    }
}
