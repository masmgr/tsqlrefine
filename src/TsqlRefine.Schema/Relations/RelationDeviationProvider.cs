using TsqlRefine.PluginSdk;

namespace TsqlRefine.Schema.Relations;

/// <summary>
/// Provides JOIN pattern deviation information by analyzing a <see cref="RelationProfile"/>
/// and exposing results through the <see cref="IRelationDeviationProvider"/> interface.
/// </summary>
public sealed class RelationDeviationProvider : IRelationDeviationProvider
{
    private readonly Dictionary<string, RelationTablePairSummary> _lookup;
    private readonly List<RelationTablePairSummary> _summaries;

    /// <summary>
    /// Creates a provider from a pre-computed <see cref="DeviationReport"/>.
    /// </summary>
    public RelationDeviationProvider(DeviationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var summaries = new List<RelationTablePairSummary>(report.Analyses.Count);
        _lookup = new Dictionary<string, RelationTablePairSummary>(
            report.Analyses.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var analysis in report.Analyses)
        {
            var summary = ConvertAnalysis(analysis);
            summaries.Add(summary);
            var key = BuildKey(
                summary.LeftSchema, summary.LeftTable,
                summary.RightSchema, summary.RightTable);
            _lookup[key] = summary;
        }

        _summaries = summaries;
    }

    /// <summary>
    /// Creates a provider from a <see cref="RelationProfile"/>, running deviation analysis.
    /// </summary>
    public static RelationDeviationProvider FromProfile(
        RelationProfile profile, DeviationThresholds? thresholds = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var report = DeviationAnalyzer.Analyze(profile, thresholds);
        return new RelationDeviationProvider(report);
    }

    /// <inheritdoc />
    public bool HasData => _summaries.Count > 0;

    /// <inheritdoc />
    public int TablePairCount => _summaries.Count;

    /// <inheritdoc />
    public RelationTablePairSummary? GetTablePairSummary(
        string leftSchema, string leftTable,
        string rightSchema, string rightTable)
    {
        // Canonicalize: ensure lexicographic order
        var leftKey = $"{leftSchema}.{leftTable}";
        var rightKey = $"{rightSchema}.{rightTable}";

        string key;
        if (string.Compare(leftKey, rightKey, StringComparison.OrdinalIgnoreCase) <= 0)
        {
            key = BuildKey(leftSchema, leftTable, rightSchema, rightTable);
        }
        else
        {
            key = BuildKey(rightSchema, rightTable, leftSchema, leftTable);
        }

        return _lookup.GetValueOrDefault(key);
    }

    /// <inheritdoc />
    public IReadOnlyList<RelationTablePairSummary> GetAllSummaries() => _summaries;

    private static string BuildKey(
        string leftSchema, string leftTable,
        string rightSchema, string rightTable) =>
        $"{leftSchema}.{leftTable}|{rightSchema}.{rightTable}";

    private static RelationTablePairSummary ConvertAnalysis(TablePairAnalysis analysis)
    {
        var deviations = new List<RelationPatternDeviation>(analysis.Deviations.Count);
        foreach (var dev in analysis.Deviations)
        {
            deviations.Add(ConvertDeviation(dev));
        }

        return new RelationTablePairSummary(
            analysis.Relation.LeftSchema,
            analysis.Relation.LeftTable,
            analysis.Relation.RightSchema,
            analysis.Relation.RightTable,
            analysis.Total,
            analysis.PatternCount,
            deviations);
    }

    private static RelationPatternDeviation ConvertDeviation(PatternDeviation dev)
    {
        var colDescs = new List<string>(dev.Pattern.ColumnPairs.Count);
        foreach (var cp in dev.Pattern.ColumnPairs)
        {
            colDescs.Add($"{cp.LeftColumn}={cp.RightColumn}");
        }

        var diffs = new List<RelationStructuralDiff>(dev.StructuralDiffs.Count);
        foreach (var diff in dev.StructuralDiffs)
        {
            diffs.Add(ConvertStructuralDiff(diff));
        }

        return new RelationPatternDeviation(
            dev.Pattern.JoinType,
            colDescs,
            dev.Pattern.OccurrenceCount,
            dev.Ratio,
            dev.DominantRatio,
            dev.Gap,
            dev.Rank,
            ConvertLevel(dev.Level),
            diffs);
    }

    private static RelationDeviationLevel ConvertLevel(DeviationLevel level) =>
        level switch
        {
            DeviationLevel.Dominant => RelationDeviationLevel.Dominant,
            DeviationLevel.Common => RelationDeviationLevel.Common,
            DeviationLevel.Rare => RelationDeviationLevel.Rare,
            DeviationLevel.VeryRare => RelationDeviationLevel.VeryRare,
            DeviationLevel.Structural => RelationDeviationLevel.Structural,
            DeviationLevel.InsufficientData => RelationDeviationLevel.InsufficientData,
            _ => RelationDeviationLevel.InsufficientData,
        };

    private static RelationStructuralDiff ConvertStructuralDiff(StructuralDiff diff) =>
        diff switch
        {
            StructuralDiff.DifferentKeyCount => RelationStructuralDiff.DifferentKeyCount,
            StructuralDiff.FunctionPresenceMismatch => RelationStructuralDiff.FunctionPresenceMismatch,
            StructuralDiff.OrPresenceMismatch => RelationStructuralDiff.OrPresenceMismatch,
            StructuralDiff.RangePresenceMismatch => RelationStructuralDiff.RangePresenceMismatch,
            StructuralDiff.DifferentJoinType => RelationStructuralDiff.DifferentJoinType,
            _ => RelationStructuralDiff.DifferentKeyCount,
        };
}
