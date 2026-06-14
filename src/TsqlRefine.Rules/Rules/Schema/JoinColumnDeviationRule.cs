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
        private readonly record struct ResolvedColumnPair(
            string LeftColumn,
            string RightColumn);

        private readonly record struct CanonicalJoinPattern(
            string LeftSchema,
            string LeftTable,
            string RightSchema,
            string RightTable,
            string JoinType,
            IReadOnlyList<string> SortedPairDescriptions);

        private readonly record struct DeviationMatchCacheKey(
            RelationTablePairSummary Summary,
            string JoinType,
            string PairSignature);

        private readonly record struct DeviationMatchResult(
            RelationPatternDeviation? Match,
            bool IsAmbiguous);

        private SchemaColumnResolver? _resolver;
        private Dictionary<RelationPatternDeviation, IReadOnlyList<string>> _sortedDeviationPairsCache =
            new(ReferenceEqualityComparer.Instance);
        private Dictionary<RelationTablePairSummary, string> _dominantInfoCache =
            new(ReferenceEqualityComparer.Instance);
        private Dictionary<DeviationMatchCacheKey, DeviationMatchResult> _deviationMatchCache =
            new(DeviationMatchCacheKeyComparer.Instance);

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.FromClause?.TableReferences is { Count: > 0 } tableRefs)
            {
                var previousResolver = _resolver;

                _resolver = new SchemaColumnResolver(schema, AliasMapBuilder.Build(tableRefs, schema));
                node.FromClause.Accept(this);

                _resolver = previousResolver;
                return;
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(QualifiedJoin node)
        {
            if (_resolver is not null && node.SearchCondition is not null)
            {
                CheckJoinColumnDeviation(node);
            }

            base.ExplicitVisit(node);
        }

        private void CheckJoinColumnDeviation(QualifiedJoin node)
        {
            if (!TryBuildCanonicalPattern(node, out var pattern))
            {
                return;
            }

            var summary = deviations.GetTablePairSummary(
                pattern.LeftSchema,
                pattern.LeftTable,
                pattern.RightSchema,
                pattern.RightTable);

            if (summary is null)
            {
                ReportUnknownTablePair(node.SecondTableReference, pattern);
                return;
            }

            var match = FindMatchingDeviation(summary, pattern.JoinType, pattern.SortedPairDescriptions);
            if (match.Match is null)
            {
                if (match.IsAmbiguous)
                {
                    // Ambiguous match — skip to avoid false positives.
                    return;
                }

                ReportUnseenPattern(node.SearchCondition!, pattern, summary);
                return;
            }

            var deviation = match.Match;
            if (IsWarnableLevel(deviation.Level))
            {
                ReportRarePattern(node.SearchCondition!, pattern, summary, deviation);
            }
        }

        private bool TryBuildCanonicalPattern(QualifiedJoin node, out CanonicalJoinPattern pattern)
        {
            pattern = default;

            var joinType = GetJoinTypeName(node.QualifiedJoinType);
            if (joinType is null)
            {
                return false;
            }

            var rawPairs = JoinEqualityPairCollector.Extract(node.SearchCondition);
            if (rawPairs.Count == 0)
            {
                return false;
            }

            var leftTable = ResolveSideTableFromPairs(node.FirstTableReference, rawPairs);
            var rightTable = ResolveSideTableFromPairs(node.SecondTableReference, rawPairs);
            if (leftTable is null || rightTable is null)
            {
                return false;
            }

            var orientedPairs = OrientColumnPairs(rawPairs, leftTable, rightTable);
            if (orientedPairs.Count == 0)
            {
                return false;
            }

            pattern = CanonicalizePattern(leftTable, rightTable, joinType, orientedPairs);
            return true;
        }

        private static CanonicalJoinPattern CanonicalizePattern(
            ResolvedTable leftTable,
            ResolvedTable rightTable,
            string joinType,
            IReadOnlyList<ResolvedColumnPair> orientedPairs)
        {
            var shouldSwap = string.Compare(
                BuildTableKey(leftTable),
                BuildTableKey(rightTable),
                StringComparison.OrdinalIgnoreCase) > 0;

            var canonicalLeft = shouldSwap ? rightTable : leftTable;
            var canonicalRight = shouldSwap ? leftTable : rightTable;
            var canonicalJoinType = shouldSwap ? SwapJoinDirection(joinType) : joinType;

            var sortedDescriptions = new List<string>(orientedPairs.Count);
            foreach (var pair in orientedPairs)
            {
                sortedDescriptions.Add(shouldSwap
                    ? $"{pair.RightColumn}={pair.LeftColumn}"
                    : $"{pair.LeftColumn}={pair.RightColumn}");
            }

            sortedDescriptions.Sort(StringComparer.OrdinalIgnoreCase);

            return new CanonicalJoinPattern(
                canonicalLeft.SchemaName,
                canonicalLeft.TableName,
                canonicalRight.SchemaName,
                canonicalRight.TableName,
                canonicalJoinType,
                sortedDescriptions);
        }

        private void ReportUnknownTablePair(TSqlFragment target, CanonicalJoinPattern pattern)
        {
            if (!deviations.HasData)
            {
                return;
            }

            AddDiagnostic(
                fragment: target,
                message: $"JOIN between {pattern.LeftSchema}.{pattern.LeftTable} and {pattern.RightSchema}.{pattern.RightTable} was not found in the relation profile.",
                code: RuleCode,
                category: Category,
                fixable: false);
        }

        private void ReportUnseenPattern(
            TSqlFragment target,
            CanonicalJoinPattern pattern,
            RelationTablePairSummary summary)
        {
            var pairDesc = string.Join(", ", pattern.SortedPairDescriptions);
            var dominantInfo = GetDominantInfo(summary);

            AddDiagnostic(
                fragment: target,
                message: $"JOIN between {pattern.LeftSchema}.{pattern.LeftTable} and {pattern.RightSchema}.{pattern.RightTable} on [{pairDesc}] was not observed in the relation profile ({summary.Total} total occurrences for this table pair).{dominantInfo}",
                code: RuleCode,
                category: Category,
                fixable: false);
        }

        private void ReportRarePattern(
            TSqlFragment target,
            CanonicalJoinPattern pattern,
            RelationTablePairSummary summary,
            RelationPatternDeviation deviation)
        {
            var pairDesc = string.Join(", ", pattern.SortedPairDescriptions);
            var levelName = deviation.Level.ToString().ToLowerInvariant();
            var pct = (deviation.Ratio * 100).ToString("F0", CultureInfo.InvariantCulture);
            var dominantInfo = GetDominantInfo(summary);

            AddDiagnostic(
                fragment: target,
                message: $"JOIN between {pattern.LeftSchema}.{pattern.LeftTable} and {pattern.RightSchema}.{pattern.RightTable} on [{pairDesc}] is {levelName} ({deviation.OccurrenceCount}/{summary.Total} occurrences, {pct}%).{dominantInfo}",
                code: RuleCode,
                category: Category,
                fixable: false);
        }

        private static bool IsWarnableLevel(RelationDeviationLevel level) =>
            level is RelationDeviationLevel.Rare
                or RelationDeviationLevel.VeryRare
                or RelationDeviationLevel.Structural;

        private DeviationMatchResult FindMatchingDeviation(
            RelationTablePairSummary summary,
            string joinType,
            IReadOnlyList<string> sortedDescriptions)
        {
            var pairSignature = BuildPairSignature(sortedDescriptions);
            var cacheKey = new DeviationMatchCacheKey(summary, joinType, pairSignature);
            if (_deviationMatchCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            RelationPatternDeviation? firstMatch = null;
            var matchCount = 0;
            foreach (var deviation in summary.Deviations)
            {
                if (!string.Equals(deviation.JoinType, joinType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!HasSameSortedPairDescriptions(GetSortedDeviationPairs(deviation), sortedDescriptions))
                {
                    continue;
                }

                matchCount++;
                if (matchCount == 1)
                {
                    firstMatch = deviation;
                    continue;
                }

                cached = new DeviationMatchResult(Match: null, IsAmbiguous: true);
                _deviationMatchCache[cacheKey] = cached;
                return cached;
            }

            cached = matchCount == 1
                ? new DeviationMatchResult(firstMatch, IsAmbiguous: false)
                : new DeviationMatchResult(Match: null, IsAmbiguous: false);
            _deviationMatchCache[cacheKey] = cached;
            return cached;
        }

        private static bool HasSameSortedPairDescriptions(
            IReadOnlyList<string> candidateDescriptions,
            IReadOnlyList<string> sortedDescriptions)
        {
            if (candidateDescriptions.Count != sortedDescriptions.Count)
            {
                return false;
            }

            for (var i = 0; i < candidateDescriptions.Count; i++)
            {
                if (!string.Equals(candidateDescriptions[i], sortedDescriptions[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static string BuildPairSignature(IReadOnlyList<string> sortedDescriptions) =>
            sortedDescriptions.Count switch
            {
                0 => string.Empty,
                1 => sortedDescriptions[0],
                _ => string.Join('\u001F', sortedDescriptions),
            };

        private IReadOnlyList<string> GetSortedDeviationPairs(RelationPatternDeviation deviation)
        {
            if (_sortedDeviationPairsCache.TryGetValue(deviation, out var cached))
            {
                return cached;
            }

            if (deviation.ColumnPairDescriptions.Count <= 1)
            {
                cached = deviation.ColumnPairDescriptions;
            }
            else
            {
                var sorted = new List<string>(deviation.ColumnPairDescriptions.Count);
                foreach (var pair in deviation.ColumnPairDescriptions)
                {
                    sorted.Add(pair);
                }

                sorted.Sort(StringComparer.OrdinalIgnoreCase);
                cached = sorted;
            }

            _sortedDeviationPairsCache[deviation] = cached;
            return cached;
        }

        private string GetDominantInfo(RelationTablePairSummary summary)
        {
            if (_dominantInfoCache.TryGetValue(summary, out var cached))
            {
                return cached;
            }

            RelationPatternDeviation? dominant = null;
            foreach (var deviation in summary.Deviations)
            {
                if (deviation.Level == RelationDeviationLevel.Dominant)
                {
                    dominant = deviation;
                    break;
                }
            }

            if (dominant is null)
            {
                cached = string.Empty;
                _dominantInfoCache[summary] = cached;
                return string.Empty;
            }

            var sortedPairs = new List<string>(dominant.ColumnPairDescriptions.Count);
            foreach (var pair in dominant.ColumnPairDescriptions)
            {
                sortedPairs.Add(pair);
            }

            sortedPairs.Sort(StringComparer.OrdinalIgnoreCase);
            var pct = (dominant.Ratio * 100).ToString("F0", CultureInfo.InvariantCulture);
            cached = $" The dominant pattern uses [{string.Join(", ", sortedPairs)}] ({pct}%).";
            _dominantInfoCache[summary] = cached;
            return cached;
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

        private static string BuildTableKey(ResolvedTable table) =>
            $"{table.SchemaName}.{table.TableName}";

        private List<ResolvedColumnPair> OrientColumnPairs(
            List<(ColumnReferenceExpression Left, ColumnReferenceExpression Right, BooleanComparisonExpression Node)> rawPairs,
            ResolvedTable leftTable,
            ResolvedTable rightTable)
        {
            var result = new List<ResolvedColumnPair>();
            foreach (var (left, right, _) in rawPairs)
            {
                var resolved1 = _resolver!.ResolveColumnToTable(left);
                var resolved2 = _resolver.ResolveColumnToTable(right);
                if (resolved1 is null || resolved2 is null)
                {
                    continue;
                }

                if (ResolvedTableComparers.TablesAreEqual(resolved1.Value.Table, leftTable) &&
                    ResolvedTableComparers.TablesAreEqual(resolved2.Value.Table, rightTable))
                {
                    result.Add(new ResolvedColumnPair(
                        resolved1.Value.ColumnName,
                        resolved2.Value.ColumnName));
                }
                else if (ResolvedTableComparers.TablesAreEqual(resolved1.Value.Table, rightTable) &&
                         ResolvedTableComparers.TablesAreEqual(resolved2.Value.Table, leftTable))
                {
                    result.Add(new ResolvedColumnPair(
                        resolved2.Value.ColumnName,
                        resolved1.Value.ColumnName));
                }
            }

            return result;
        }

        private ResolvedTable? ResolveSideTableFromPairs(
            TableReference tableRef,
            List<(ColumnReferenceExpression Left, ColumnReferenceExpression Right, BooleanComparisonExpression Node)> rawPairs)
        {
            var candidates = ResolveNamedTables(tableRef);
            if (candidates.Count == 0)
            {
                return null;
            }

            if (candidates.Count == 1)
            {
                return candidates[0];
            }

            var referenced = new List<ResolvedTable>();

            foreach (var (left, right, _) in rawPairs)
            {
                var leftResolved = _resolver!.ResolveColumnToTable(left);
                if (leftResolved is not null)
                {
                    TryAddReferencedCandidate(referenced, candidates, leftResolved.Value.Table);
                }

                var rightResolved = _resolver.ResolveColumnToTable(right);
                if (rightResolved is not null)
                {
                    TryAddReferencedCandidate(referenced, candidates, rightResolved.Value.Table);
                }
            }

            if (referenced.Count == 1)
            {
                return referenced[0];
            }

            if (referenced.Count > 1)
            {
                return null;
            }

            return ResolveFallbackSideTable(tableRef);
        }

        private List<ResolvedTable> ResolveNamedTables(TableReference tableRef)
        {
            var tables = new List<ResolvedTable>();
            CollectResolvedNamedTables(tableRef, tables);
            return tables;
        }

        private void CollectResolvedNamedTables(TableReference tableRef, List<ResolvedTable> tables)
        {
            switch (tableRef)
            {
                case NamedTableReference named:
                    var alias = named.Alias?.Value ?? named.SchemaObject.BaseIdentifier?.Value;
                    if (alias is not null &&
                        _resolver?.AliasMap.TryResolve(alias, out var resolved) == true &&
                        resolved is not null)
                    {
                        TryAddUniqueTable(tables, resolved);
                    }

                    return;

                case JoinTableReference join:
                    CollectResolvedNamedTables(join.FirstTableReference, tables);
                    CollectResolvedNamedTables(join.SecondTableReference, tables);
                    return;

                case JoinParenthesisTableReference joinParen when joinParen.Join is not null:
                    CollectResolvedNamedTables(joinParen.Join, tables);
                    return;
            }
        }

        private static void TryAddReferencedCandidate(
            List<ResolvedTable> referenced,
            IReadOnlyList<ResolvedTable> candidates,
            ResolvedTable table)
        {
            foreach (var candidate in candidates)
            {
                if (ResolvedTableComparers.TablesAreEqual(candidate, table))
                {
                    TryAddUniqueTable(referenced, candidate);
                    return;
                }
            }
        }

        private static void TryAddUniqueTable(List<ResolvedTable> tables, ResolvedTable table)
        {
            for (var i = 0; i < tables.Count; i++)
            {
                if (ResolvedTableComparers.TablesAreEqual(tables[i], table))
                {
                    return;
                }
            }

            tables.Add(table);
        }

        private ResolvedTable? ResolveFallbackSideTable(TableReference tableRef)
        {
            if (tableRef is NamedTableReference named)
            {
                var alias = named.Alias?.Value
                    ?? named.SchemaObject.BaseIdentifier?.Value;
                if (alias is not null && _resolver?.AliasMap.TryResolve(alias, out var resolved) == true)
                {
                    return resolved;
                }
            }
            else if (tableRef is JoinTableReference join)
            {
                return ResolveFallbackSideTable(join.SecondTableReference)
                    ?? ResolveFallbackSideTable(join.FirstTableReference);
            }
            else if (tableRef is JoinParenthesisTableReference joinParen && joinParen.Join is not null)
            {
                return ResolveFallbackSideTable(joinParen.Join);
            }

            return null;
        }

        private sealed class DeviationMatchCacheKeyComparer : IEqualityComparer<DeviationMatchCacheKey>
        {
            public static DeviationMatchCacheKeyComparer Instance { get; } = new();

            public bool Equals(DeviationMatchCacheKey x, DeviationMatchCacheKey y) =>
                ReferenceEquals(x.Summary, y.Summary)
                && string.Equals(x.JoinType, y.JoinType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.PairSignature, y.PairSignature, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode(DeviationMatchCacheKey obj)
            {
                var hash = new HashCode();
                hash.Add(ReferenceEqualityComparer.Instance.GetHashCode(obj.Summary));
                hash.Add(obj.JoinType, StringComparer.OrdinalIgnoreCase);
                hash.Add(obj.PairSignature, StringComparer.OrdinalIgnoreCase);
                return hash.ToHashCode();
            }
        }
    }
}
