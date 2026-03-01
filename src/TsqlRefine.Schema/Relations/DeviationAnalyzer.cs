namespace TsqlRefine.Schema.Relations;

/// <summary>
/// Analyzes a <see cref="RelationProfile"/> to detect JOIN pattern deviations
/// based on frequency and structural differences from the dominant pattern.
/// </summary>
public static class DeviationAnalyzer
{
    /// <summary>
    /// Analyzes all table relations in a profile and produces deviation results.
    /// </summary>
    /// <param name="profile">The aggregated relation profile to analyze.</param>
    /// <param name="thresholds">Optional deviation thresholds. Uses defaults if null.</param>
    /// <returns>A deviation report covering all table pairs.</returns>
    public static DeviationReport Analyze(
        RelationProfile profile,
        DeviationThresholds? thresholds = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var t = thresholds ?? new DeviationThresholds();

        var analyses = new List<TablePairAnalysis>(profile.Relations.Count);

        foreach (var relation in profile.Relations)
        {
            analyses.Add(AnalyzeTablePair(relation, t));
        }

        return new DeviationReport(t, analyses);
    }

    /// <summary>
    /// Analyzes a single table pair and produces per-pattern deviation results.
    /// </summary>
    internal static TablePairAnalysis AnalyzeTablePair(
        TableRelation relation, DeviationThresholds thresholds)
    {
        var patterns = relation.Patterns;
        var total = 0;
        for (var i = 0; i < patterns.Count; i++)
        {
            total += patterns[i].OccurrenceCount;
        }

        // Patterns are already sorted by OccurrenceCount descending in RelationAggregator
        var dominant = patterns.Count > 0 ? patterns[0] : null;
        var dominantRatio = dominant is not null ? (double)dominant.OccurrenceCount / total : 0.0;

        var deviations = new List<PatternDeviation>(patterns.Count);

        for (var i = 0; i < patterns.Count; i++)
        {
            var pattern = patterns[i];
            var ratio = (double)pattern.OccurrenceCount / total;
            var gap = dominantRatio - ratio;
            var rank = i + 1;

            StructuralDiff[] structuralDiffs = dominant is not null && rank > 1
                ? DetectStructuralDiffs(pattern, dominant)
                : [];

            var level = ClassifyDeviation(ratio, rank, total, structuralDiffs, thresholds);

            deviations.Add(new PatternDeviation(
                pattern, ratio, dominantRatio, gap, rank, level, structuralDiffs));
        }

        return new TablePairAnalysis(relation, total, patterns.Count, deviations);
    }

    private static DeviationLevel ClassifyDeviation(
        double ratio,
        int rank,
        int total,
        StructuralDiff[] structuralDiffs,
        DeviationThresholds thresholds)
    {
        if (rank == 1)
        {
            return DeviationLevel.Dominant;
        }

        if (total < thresholds.MinTotal)
        {
            return DeviationLevel.InsufficientData;
        }

        // Structural deviations take priority over frequency-based classification
        if (structuralDiffs.Length > 0)
        {
            return DeviationLevel.Structural;
        }

        if (ratio < thresholds.VeryRareThreshold)
        {
            return DeviationLevel.VeryRare;
        }

        if (ratio < thresholds.RareThreshold)
        {
            return DeviationLevel.Rare;
        }

        return DeviationLevel.Common;
    }

    private static StructuralDiff[] DetectStructuralDiffs(
        JoinPattern pattern, JoinPattern dominant)
    {
        var diffs = new List<StructuralDiff>(4);

        if (pattern.ColumnPairs.Count != dominant.ColumnPairs.Count)
        {
            diffs.Add(StructuralDiff.DifferentKeyCount);
        }

        if (!string.Equals(pattern.JoinType, dominant.JoinType, StringComparison.OrdinalIgnoreCase))
        {
            diffs.Add(StructuralDiff.DifferentJoinType);
        }

        CheckFlagMismatch(diffs, pattern.ShapeFlags, dominant.ShapeFlags,
            JoinShape.ContainsFunction, StructuralDiff.FunctionPresenceMismatch);
        CheckFlagMismatch(diffs, pattern.ShapeFlags, dominant.ShapeFlags,
            JoinShape.ContainsOr, StructuralDiff.OrPresenceMismatch);
        CheckFlagMismatch(diffs, pattern.ShapeFlags, dominant.ShapeFlags,
            JoinShape.ContainsRange, StructuralDiff.RangePresenceMismatch);

        return diffs.Count > 0 ? [.. diffs] : [];
    }

    private static void CheckFlagMismatch(
        List<StructuralDiff> diffs,
        JoinShape patternFlags,
        JoinShape dominantFlags,
        JoinShape flag,
        StructuralDiff diff)
    {
        var patternHasFlag = (patternFlags & flag) != 0;
        var dominantHasFlag = (dominantFlags & flag) != 0;
        if (patternHasFlag != dominantHasFlag)
        {
            diffs.Add(diff);
        }
    }
}
