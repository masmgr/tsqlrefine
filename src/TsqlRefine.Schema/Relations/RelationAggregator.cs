namespace TsqlRefine.Schema.Relations;

/// <summary>
/// Aggregates raw JOIN occurrences into a <see cref="RelationProfile"/>.
/// </summary>
internal static class RelationAggregator
{
    /// <summary>
    /// Aggregates raw JOIN occurrences, canonicalizing table pairs and counting patterns.
    /// </summary>
    internal static RelationProfile Aggregate(IEnumerable<RawJoinInfo> rawJoins, int fileCount)
    {
        var joinList = rawJoins as IList<RawJoinInfo> ?? rawJoins.ToList();
        var totalJoinCount = joinList.Count;

        // Group by canonicalized table pair
        var tablePairGroups = new Dictionary<string, List<CanonicalJoin>>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in joinList)
        {
            var canonical = Canonicalize(raw);
            var key = $"{canonical.LeftSchema}.{canonical.LeftTable}|{canonical.RightSchema}.{canonical.RightTable}";

            if (!tablePairGroups.TryGetValue(key, out var list))
            {
                list = [];
                tablePairGroups[key] = list;
            }

            list.Add(canonical);
        }

        // Build relations
        var relations = new List<TableRelation>(tablePairGroups.Count);

        foreach (var (_, joins) in tablePairGroups.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            var first = joins[0];

            // Group by pattern (join type + column pairs)
            var patternGroups = new Dictionary<string, PatternAccumulator>(StringComparer.Ordinal);

            foreach (var join in joins)
            {
                var patternKey = BuildPatternKey(join.JoinType, join.ColumnPairs);
                if (!patternGroups.TryGetValue(patternKey, out var acc))
                {
                    acc = new PatternAccumulator(join.JoinType, join.ColumnPairs);
                    patternGroups[patternKey] = acc;
                }

                acc.Count++;
                acc.SourceFiles.Add(join.SourceFile);
            }

            var patterns = patternGroups.Values
                .OrderByDescending(p => p.Count)
                .ThenBy(p => p.JoinType, StringComparer.OrdinalIgnoreCase)
                .Select(p => new JoinPattern(
                    p.JoinType,
                    p.ColumnPairs,
                    p.Count,
                    p.SourceFiles.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList()))
                .ToList();

            relations.Add(new TableRelation(
                first.LeftSchema,
                first.LeftTable,
                first.RightSchema,
                first.RightTable,
                patterns));
        }

        var hash = RelationProfileSerializer.ComputeContentHash(relations);
        var metadata = new RelationProfileMetadata(
            GeneratedAt: DateTime.UtcNow.ToString("O"),
            FileCount: fileCount,
            TotalJoinCount: totalJoinCount,
            ContentHash: hash);

        return new RelationProfile(metadata, relations);
    }

    /// <summary>
    /// Canonicalizes a raw join so that table pairs are in lexicographic order.
    /// When swapping, column pairs are also swapped.
    /// </summary>
    private static CanonicalJoin Canonicalize(RawJoinInfo raw)
    {
        var leftKey = $"{raw.LeftSchema}.{raw.LeftTable}";
        var rightKey = $"{raw.RightSchema}.{raw.RightTable}";

        if (string.Compare(leftKey, rightKey, StringComparison.OrdinalIgnoreCase) <= 0)
        {
            return new CanonicalJoin(
                raw.LeftSchema, raw.LeftTable,
                raw.RightSchema, raw.RightTable,
                raw.JoinType, raw.ColumnPairs, raw.SourceFile);
        }

        // Swap tables and column pair sides
        var swappedPairs = raw.ColumnPairs
            .Select(cp => new ColumnPair(cp.RightColumn, cp.LeftColumn))
            .ToList();
        var swappedJoinType = SwapJoinDirection(raw.JoinType);

        return new CanonicalJoin(
            raw.RightSchema, raw.RightTable,
            raw.LeftSchema, raw.LeftTable,
            swappedJoinType, swappedPairs, raw.SourceFile);
    }

    private static string SwapJoinDirection(string joinType) =>
        joinType.ToUpperInvariant() switch
        {
            "LEFT" => "RIGHT",
            "RIGHT" => "LEFT",
            _ => joinType,
        };

    private static string BuildPatternKey(string joinType, IReadOnlyList<ColumnPair> columnPairs)
    {
        var sortedPairs = columnPairs
            .OrderBy(cp => cp.LeftColumn, StringComparer.OrdinalIgnoreCase)
            .ThenBy(cp => cp.RightColumn, StringComparer.OrdinalIgnoreCase)
            .Select(cp => $"{cp.LeftColumn}={cp.RightColumn}");

        return $"{joinType}:{string.Join(",", sortedPairs)}";
    }

    private sealed record CanonicalJoin(
        string LeftSchema,
        string LeftTable,
        string RightSchema,
        string RightTable,
        string JoinType,
        IReadOnlyList<ColumnPair> ColumnPairs,
        string SourceFile);

    private sealed class PatternAccumulator(string joinType, IReadOnlyList<ColumnPair> columnPairs)
    {
        public string JoinType { get; } = joinType;
        public IReadOnlyList<ColumnPair> ColumnPairs { get; } = columnPairs;
        public int Count { get; set; }
        public HashSet<string> SourceFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
