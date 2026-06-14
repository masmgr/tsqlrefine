namespace TsqlRefine.Schema.Relations;

/// <summary>
/// Configuration thresholds for deviation detection.
/// </summary>
/// <param name="MinTotal">Minimum total occurrences for a table pair to be analyzed. Below this, results are informational only.</param>
/// <param name="RareThreshold">Patterns with ratio below this are classified as rare (0.0–1.0).</param>
/// <param name="VeryRareThreshold">Patterns with ratio below this are classified as very rare (0.0–1.0).</param>
public sealed record DeviationThresholds(
    int MinTotal = 10,
    double RareThreshold = 0.1,
    double VeryRareThreshold = 0.03
);

/// <summary>
/// Classification of how unusual a JOIN pattern is compared to the norm for a given table pair.
/// </summary>
public enum DeviationLevel
{
    /// <summary>This is the dominant (most frequent) pattern — no deviation.</summary>
    Dominant,

    /// <summary>Common enough to not be flagged (ratio above rare threshold).</summary>
    Common,

    /// <summary>Occurs less than the rare threshold (default 10%).</summary>
    Rare,

    /// <summary>Occurs less than the very-rare threshold (default 3%).</summary>
    VeryRare,

    /// <summary>Structurally different from the dominant pattern (different key count or shape flags).</summary>
    Structural,

    /// <summary>Insufficient data to classify (table pair total below min total).</summary>
    InsufficientData,
}

/// <summary>
/// Describes a specific structural difference between a pattern and the dominant pattern.
/// </summary>
public enum StructuralDiff
{
    /// <summary>Different number of key columns compared to the dominant pattern.</summary>
    DifferentKeyCount,

    /// <summary>This pattern contains function calls while the dominant does not (or vice versa).</summary>
    FunctionPresenceMismatch,

    /// <summary>This pattern contains OR while the dominant does not (or vice versa).</summary>
    OrPresenceMismatch,

    /// <summary>This pattern contains range conditions while the dominant does not (or vice versa).</summary>
    RangePresenceMismatch,

    /// <summary>This pattern uses a different JOIN type than the dominant.</summary>
    DifferentJoinType,
}

/// <summary>
/// Deviation analysis result for a single JOIN pattern within a table pair.
/// </summary>
/// <param name="Pattern">The JOIN pattern being analyzed.</param>
/// <param name="Ratio">Occurrence ratio: count / total for this table pair (0.0–1.0).</param>
/// <param name="DominantRatio">Ratio of the most common pattern for this table pair.</param>
/// <param name="Gap">Distance from the dominant ratio: dominantRatio − ratio.</param>
/// <param name="Rank">1-based rank among patterns for this table pair (1 = most common).</param>
/// <param name="Level">Classification level of this deviation.</param>
/// <param name="StructuralDiffs">Structural differences from the dominant pattern, if any.</param>
public sealed record PatternDeviation(
    JoinPattern Pattern,
    double Ratio,
    double DominantRatio,
    double Gap,
    int Rank,
    DeviationLevel Level,
    IReadOnlyList<StructuralDiff> StructuralDiffs
);

/// <summary>
/// Deviation analysis result for a single table pair, containing analysis of all its patterns.
/// </summary>
/// <param name="Relation">The table relation being analyzed.</param>
/// <param name="Total">Total occurrence count across all patterns for this table pair.</param>
/// <param name="PatternCount">Number of distinct patterns for this table pair.</param>
/// <param name="Deviations">Deviation analysis for each pattern, ordered by ratio descending.</param>
public sealed record TablePairAnalysis(
    TableRelation Relation,
    int Total,
    int PatternCount,
    IReadOnlyList<PatternDeviation> Deviations
);

/// <summary>
/// Complete deviation analysis report for all table pairs in a relation profile.
/// </summary>
/// <param name="Thresholds">The thresholds used for this analysis.</param>
/// <param name="Analyses">Per-table-pair analysis results.</param>
public sealed record DeviationReport(
    DeviationThresholds Thresholds,
    IReadOnlyList<TablePairAnalysis> Analyses
);
