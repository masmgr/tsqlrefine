using System.Collections.Frozen;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Schema.Relations;

/// <summary>
/// Provides JOIN pattern deviation information by analyzing a <see cref="RelationProfile"/>
/// and exposing results through the <see cref="IRelationDeviationProvider"/> interface.
/// </summary>
public sealed class RelationDeviationProvider : IRelationDeviationProvider
{
    private readonly FrozenDictionary<TablePairKey, RelationTablePairSummary> _lookup;
    private readonly List<RelationTablePairSummary> _summaries;

    /// <summary>
    /// Creates a provider from a pre-computed <see cref="DeviationReport"/>.
    /// </summary>
    public RelationDeviationProvider(DeviationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        var summaries = new List<RelationTablePairSummary>(report.Analyses.Count);
        var lookupBuilder = new Dictionary<TablePairKey, RelationTablePairSummary>(
            report.Analyses.Count, TablePairKeyComparer.Instance);

        foreach (var analysis in report.Analyses)
        {
            var summary = ConvertAnalysis(analysis);
            summaries.Add(summary);
            var key = CreateCanonicalKey(
                summary.LeftSchema, summary.LeftTable,
                summary.RightSchema, summary.RightTable);
            lookupBuilder[key] = summary;
        }

        _lookup = lookupBuilder.ToFrozenDictionary(TablePairKeyComparer.Instance);
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
        var key = CreateCanonicalKey(leftSchema, leftTable, rightSchema, rightTable);
        return _lookup.GetValueOrDefault(key);
    }

    /// <inheritdoc />
    public IReadOnlyList<RelationTablePairSummary> GetAllSummaries() => _summaries;

    private static TablePairKey CreateCanonicalKey(
        string leftSchema, string leftTable,
        string rightSchema, string rightTable)
    {
        if (IsCanonicalOrder(leftSchema, leftTable, rightSchema, rightTable))
        {
            return new TablePairKey(leftSchema, leftTable, rightSchema, rightTable);
        }

        return new TablePairKey(rightSchema, rightTable, leftSchema, leftTable);
    }

    private static bool IsCanonicalOrder(
        string leftSchema, string leftTable,
        string rightSchema, string rightTable)
    {
        var schemaComparison = string.Compare(
            leftSchema, rightSchema, StringComparison.OrdinalIgnoreCase);
        if (schemaComparison != 0)
        {
            return schemaComparison < 0;
        }

        return string.Compare(leftTable, rightTable, StringComparison.OrdinalIgnoreCase) <= 0;
    }

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

    private readonly record struct TablePairKey(
        string LeftSchema,
        string LeftTable,
        string RightSchema,
        string RightTable);

    private sealed class TablePairKeyComparer : IEqualityComparer<TablePairKey>
    {
        public static TablePairKeyComparer Instance { get; } = new();

        public bool Equals(TablePairKey x, TablePairKey y) =>
            string.Equals(x.LeftSchema, y.LeftSchema, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.LeftTable, y.LeftTable, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.RightSchema, y.RightSchema, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.RightTable, y.RightTable, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(TablePairKey obj)
        {
            var hash = new HashCode();
            hash.Add(obj.LeftSchema, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.LeftTable, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.RightSchema, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.RightTable, StringComparer.OrdinalIgnoreCase);
            return hash.ToHashCode();
        }
    }
}
