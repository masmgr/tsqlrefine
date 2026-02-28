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
        private readonly record struct RawColumnPair(
            ColumnReferenceExpression Left,
            ColumnReferenceExpression Right);

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

        private AliasMap? _currentAliasMap;
        private Dictionary<ColumnReferenceExpression, (ResolvedTable Table, string ColumnName)?> _resolvedColumnCache =
            new(ReferenceEqualityComparer.Instance);
        private Dictionary<(ResolvedTable Table, string ColumnName), bool> _columnExistsCache =
            new(ResolvedTableColumnKeyComparer.Instance);
        private Dictionary<string, (ResolvedTable Table, string ColumnName)?> _unqualifiedColumnResolutionCache =
            new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<RelationPatternDeviation, IReadOnlyList<string>> _sortedDeviationPairsCache =
            new(ReferenceEqualityComparer.Instance);
        private Dictionary<RelationTablePairSummary, string> _dominantInfoCache =
            new(ReferenceEqualityComparer.Instance);

        public override void ExplicitVisit(QuerySpecification node)
        {
            if (node.FromClause?.TableReferences is { Count: > 0 } tableRefs)
            {
                var previousMap = _currentAliasMap;
                var previousResolvedColumnCache = _resolvedColumnCache;
                var previousColumnExistsCache = _columnExistsCache;
                var previousUnqualifiedColumnResolutionCache = _unqualifiedColumnResolutionCache;
                var previousSortedDeviationPairsCache = _sortedDeviationPairsCache;
                var previousDominantInfoCache = _dominantInfoCache;

                _currentAliasMap = AliasMapBuilder.Build(tableRefs, schema);
                _resolvedColumnCache = new Dictionary<ColumnReferenceExpression, (ResolvedTable Table, string ColumnName)?>(ReferenceEqualityComparer.Instance);
                _columnExistsCache = new Dictionary<(ResolvedTable Table, string ColumnName), bool>(ResolvedTableColumnKeyComparer.Instance);
                _unqualifiedColumnResolutionCache = new Dictionary<string, (ResolvedTable Table, string ColumnName)?>(StringComparer.OrdinalIgnoreCase);
                _sortedDeviationPairsCache = new Dictionary<RelationPatternDeviation, IReadOnlyList<string>>(ReferenceEqualityComparer.Instance);
                _dominantInfoCache = new Dictionary<RelationTablePairSummary, string>(ReferenceEqualityComparer.Instance);

                node.FromClause.Accept(this);

                _currentAliasMap = previousMap;
                _resolvedColumnCache = previousResolvedColumnCache;
                _columnExistsCache = previousColumnExistsCache;
                _unqualifiedColumnResolutionCache = previousUnqualifiedColumnResolutionCache;
                _sortedDeviationPairsCache = previousSortedDeviationPairsCache;
                _dominantInfoCache = previousDominantInfoCache;
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

            var matches = FindMatchingDeviations(summary, pattern.JoinType, pattern.SortedPairDescriptions);
            if (matches.Count == 0)
            {
                ReportUnseenPattern(node.SearchCondition!, pattern, summary);
                return;
            }

            if (matches.Count > 1)
            {
                // Ambiguous match — skip to avoid false positives.
                return;
            }

            var deviation = matches[0];
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

            var rawPairs = ExtractEqualityPairs(node.SearchCondition);
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

        private List<RelationPatternDeviation> FindMatchingDeviations(
            RelationTablePairSummary summary,
            string joinType,
            IReadOnlyList<string> sortedDescriptions)
        {
            var matches = new List<RelationPatternDeviation>();

            foreach (var deviation in summary.Deviations)
            {
                if (!string.Equals(deviation.JoinType, joinType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (HasSameSortedPairDescriptions(GetSortedDeviationPairs(deviation), sortedDescriptions))
                {
                    matches.Add(deviation);
                }
            }

            return matches;
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

        private static List<RawColumnPair> ExtractEqualityPairs(BooleanExpression? condition)
        {
            var results = new List<RawColumnPair>();
            CollectEqualityPairs(condition, results);
            return results;
        }

        private static void CollectEqualityPairs(
            BooleanExpression? condition,
            List<RawColumnPair> results)
        {
            switch (condition)
            {
                case BooleanComparisonExpression { ComparisonType: BooleanComparisonType.Equals } comparison
                    when comparison.FirstExpression is ColumnReferenceExpression leftCol
                      && comparison.SecondExpression is ColumnReferenceExpression rightCol:
                    results.Add(new RawColumnPair(leftCol, rightCol));
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
            if (_resolvedColumnCache.TryGetValue(colRef, out var cached))
            {
                return cached;
            }

            var resolved = ResolveColumnToTableCore(colRef);
            _resolvedColumnCache[colRef] = resolved;
            return resolved;
        }

        private (ResolvedTable Table, string ColumnName)? ResolveColumnToTableCore(ColumnReferenceExpression colRef)
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
                if (QualifierLookupKeyBuilder.TryResolve(_currentAliasMap, identifiers, out var resolved))
                {
                    return resolved is null ? null : (resolved, columnName);
                }

                return null;
            }

            if (_unqualifiedColumnResolutionCache.TryGetValue(columnName, out var unqualifiedCached))
            {
                return unqualifiedCached;
            }

            foreach (var table in _currentAliasMap.AllTables)
            {
                if (ColumnExists(table, columnName))
                {
                    var result = (table, columnName);
                    _unqualifiedColumnResolutionCache[columnName] = result;
                    return result;
                }
            }

            _unqualifiedColumnResolutionCache[columnName] = null;
            return null;
        }

        private bool ColumnExists(ResolvedTable table, string columnName)
        {
            if (_columnExistsCache.TryGetValue((table, columnName), out var exists))
            {
                return exists;
            }

            exists = schema.ResolveColumn(table, columnName) is not null;
            _columnExistsCache[(table, columnName)] = exists;
            return exists;
        }

        /// <summary>
        /// Orients column pairs so Left belongs to leftTable and Right to rightTable,
        /// regardless of the order they appear in the ON clause.
        /// </summary>
        private List<ResolvedColumnPair> OrientColumnPairs(
            IReadOnlyList<RawColumnPair> rawPairs,
            ResolvedTable leftTable,
            ResolvedTable rightTable)
        {
            var result = new List<ResolvedColumnPair>();
            foreach (var pair in rawPairs)
            {
                var resolved1 = ResolveColumnToTable(pair.Left);
                var resolved2 = ResolveColumnToTable(pair.Right);
                if (resolved1 is null || resolved2 is null)
                {
                    continue;
                }

                if (TablesAreEqual(resolved1.Value.Table, leftTable) &&
                    TablesAreEqual(resolved2.Value.Table, rightTable))
                {
                    result.Add(new ResolvedColumnPair(
                        resolved1.Value.ColumnName,
                        resolved2.Value.ColumnName));
                }
                else if (TablesAreEqual(resolved1.Value.Table, rightTable) &&
                         TablesAreEqual(resolved2.Value.Table, leftTable))
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
            IReadOnlyList<RawColumnPair> rawPairs)
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

            foreach (var pair in rawPairs)
            {
                var leftResolved = ResolveColumnToTable(pair.Left);
                if (leftResolved is not null)
                {
                    TryAddReferencedCandidate(referenced, candidates, leftResolved.Value.Table);
                }

                var rightResolved = ResolveColumnToTable(pair.Right);
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
                // Ambiguous side resolution in nested joins — skip to avoid false positives.
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
                        _currentAliasMap?.TryResolve(alias, out var resolved) == true &&
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
                if (TablesAreEqual(candidate, table))
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
                if (TablesAreEqual(tables[i], table))
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
                if (alias is not null && _currentAliasMap?.TryResolve(alias, out var resolved) == true)
                {
                    return resolved;
                }
            }
            else if (tableRef is JoinTableReference join)
            {
                // For nested JOINs, prefer the rightmost (second) table.
                return ResolveFallbackSideTable(join.SecondTableReference)
                    ?? ResolveFallbackSideTable(join.FirstTableReference);
            }
            else if (tableRef is JoinParenthesisTableReference joinParen && joinParen.Join is not null)
            {
                return ResolveFallbackSideTable(joinParen.Join);
            }

            return null;
        }

        private static bool TablesAreEqual(ResolvedTable a, ResolvedTable b) =>
            string.Equals(a.SchemaName, b.SchemaName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(a.TableName, b.TableName, StringComparison.OrdinalIgnoreCase);

        private sealed class ResolvedTableColumnKeyComparer : IEqualityComparer<(ResolvedTable Table, string ColumnName)>
        {
            public static ResolvedTableColumnKeyComparer Instance { get; } = new();

            public bool Equals((ResolvedTable Table, string ColumnName) x, (ResolvedTable Table, string ColumnName) y) =>
                TablesAreEqual(x.Table, y.Table)
                && string.Equals(x.ColumnName, y.ColumnName, StringComparison.OrdinalIgnoreCase);

            public int GetHashCode((ResolvedTable Table, string ColumnName) obj)
            {
                var hash = new HashCode();
                hash.Add(obj.Table.SchemaName, StringComparer.OrdinalIgnoreCase);
                hash.Add(obj.Table.TableName, StringComparer.OrdinalIgnoreCase);
                hash.Add(obj.ColumnName, StringComparer.OrdinalIgnoreCase);
                return hash.ToHashCode();
            }
        }
    }
}
