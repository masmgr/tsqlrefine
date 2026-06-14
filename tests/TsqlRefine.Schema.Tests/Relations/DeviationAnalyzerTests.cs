using TsqlRefine.Schema.Relations;

namespace TsqlRefine.Schema.Tests.Relations;

public sealed class DeviationAnalyzerTests
{
    private static RelationProfile BuildProfile(
        params (string JoinType, int Count, JoinShape Flags, string[] Cols)[] patterns)
    {
        var joinPatterns = patterns.Select((p, i) =>
            new JoinPattern(
                p.JoinType,
                Enumerable.Range(0, p.Cols.Length / 2)
                    .Select(j => new ColumnPair(p.Cols[j * 2], p.Cols[j * 2 + 1]))
                    .ToList(),
                p.Count,
                [$"file{i}.sql"],
                p.Flags))
            .OrderByDescending(p => p.OccurrenceCount)
            .ToList();

        var relation = new TableRelation("dbo", "A", "dbo", "B", joinPatterns);
        var metadata = new RelationProfileMetadata(
            DateTime.UtcNow.ToString("O"), 1, joinPatterns.Sum(p => p.OccurrenceCount), "test");
        return new RelationProfile(metadata, [relation]);
    }

    [Fact]
    public void Analyze_SinglePattern_Dominant()
    {
        var profile = BuildProfile(
            ("INNER", 50, JoinShape.None, ["Id", "AId"]));

        var report = DeviationAnalyzer.Analyze(profile);

        Assert.Single(report.Analyses);
        Assert.Single(report.Analyses[0].Deviations);
        Assert.Equal(DeviationLevel.Dominant, report.Analyses[0].Deviations[0].Level);
        Assert.Equal(1.0, report.Analyses[0].Deviations[0].Ratio);
    }

    [Fact]
    public void Analyze_RarePattern_ClassifiesAsRare()
    {
        // 92 + 8 = 100; 8/100 = 0.08 -> rare (< 0.1)
        var profile = BuildProfile(
            ("INNER", 92, JoinShape.None, ["Id", "AId"]),
            ("INNER", 8, JoinShape.None, ["Code", "Code"]));

        var report = DeviationAnalyzer.Analyze(profile);

        Assert.Equal(2, report.Analyses[0].Deviations.Count);
        Assert.Equal(DeviationLevel.Dominant, report.Analyses[0].Deviations[0].Level);
        Assert.Equal(DeviationLevel.Rare, report.Analyses[0].Deviations[1].Level);
    }

    [Fact]
    public void Analyze_VeryRarePattern_ClassifiesAsVeryRare()
    {
        // 98 + 2 = 100; 2/100 = 0.02 -> very rare (< 0.03)
        var profile = BuildProfile(
            ("INNER", 98, JoinShape.None, ["Id", "AId"]),
            ("INNER", 2, JoinShape.None, ["Code", "Code"]));

        var report = DeviationAnalyzer.Analyze(profile);

        Assert.Equal(DeviationLevel.VeryRare, report.Analyses[0].Deviations[1].Level);
    }

    [Fact]
    public void Analyze_InsufficientData_BelowMinTotal()
    {
        // 5 + 1 = 6 total, below default minTotal of 10
        var profile = BuildProfile(
            ("INNER", 5, JoinShape.None, ["Id", "AId"]),
            ("INNER", 1, JoinShape.None, ["Code", "Code"]));

        var report = DeviationAnalyzer.Analyze(profile);

        Assert.Equal(DeviationLevel.Dominant, report.Analyses[0].Deviations[0].Level);
        Assert.Equal(DeviationLevel.InsufficientData, report.Analyses[0].Deviations[1].Level);
    }

    [Fact]
    public void Analyze_StructuralDeviation_DifferentKeyCount()
    {
        // Dominant has 1 key, second has 2 keys
        var profile = BuildProfile(
            ("INNER", 80, JoinShape.None, ["Id", "AId"]),
            ("INNER", 20, JoinShape.None, ["Id", "AId", "Code", "Code"]));

        var report = DeviationAnalyzer.Analyze(profile);

        Assert.Equal(DeviationLevel.Structural, report.Analyses[0].Deviations[1].Level);
        Assert.Contains(StructuralDiff.DifferentKeyCount,
            report.Analyses[0].Deviations[1].StructuralDiffs);
    }

    [Fact]
    public void Analyze_StructuralDeviation_FunctionMismatch()
    {
        var profile = BuildProfile(
            ("INNER", 80, JoinShape.None, ["Id", "AId"]),
            ("INNER", 20, JoinShape.ContainsFunction, ["Id", "AId"]));

        var report = DeviationAnalyzer.Analyze(profile);

        Assert.Equal(DeviationLevel.Structural, report.Analyses[0].Deviations[1].Level);
        Assert.Contains(StructuralDiff.FunctionPresenceMismatch,
            report.Analyses[0].Deviations[1].StructuralDiffs);
    }

    [Fact]
    public void Analyze_StructuralDeviation_OverridesRareClassification()
    {
        // Ratio is 20% (above rare threshold), but structural diff should classify as Structural
        var profile = BuildProfile(
            ("INNER", 80, JoinShape.None, ["Id", "AId"]),
            ("INNER", 20, JoinShape.ContainsOr, ["Id", "AId"]));

        var report = DeviationAnalyzer.Analyze(profile);

        Assert.Equal(DeviationLevel.Structural, report.Analyses[0].Deviations[1].Level);
    }

    [Fact]
    public void Analyze_CommonPattern_NotFlagged()
    {
        // 60 + 40 = 100; 40/100 = 0.40 -> common (above 10%)
        var profile = BuildProfile(
            ("INNER", 60, JoinShape.None, ["Id", "AId"]),
            ("INNER", 40, JoinShape.None, ["Code", "Code"]));

        var report = DeviationAnalyzer.Analyze(profile);

        Assert.Equal(DeviationLevel.Common, report.Analyses[0].Deviations[1].Level);
    }

    [Fact]
    public void Analyze_CustomThresholds_Respected()
    {
        var thresholds = new DeviationThresholds(MinTotal: 5, RareThreshold: 0.3, VeryRareThreshold: 0.1);
        // 8 + 2 = 10; 2/10 = 0.20 -> rare with 0.3 threshold
        var profile = BuildProfile(
            ("INNER", 8, JoinShape.None, ["Id", "AId"]),
            ("INNER", 2, JoinShape.None, ["Code", "Code"]));

        var report = DeviationAnalyzer.Analyze(profile, thresholds);

        Assert.Equal(DeviationLevel.Rare, report.Analyses[0].Deviations[1].Level);
    }

    [Fact]
    public void Analyze_RatioAndGap_CorrectlyComputed()
    {
        var profile = BuildProfile(
            ("INNER", 70, JoinShape.None, ["Id", "AId"]),
            ("INNER", 30, JoinShape.None, ["Code", "Code"]));

        var report = DeviationAnalyzer.Analyze(profile);

        var dominant = report.Analyses[0].Deviations[0];
        var minor = report.Analyses[0].Deviations[1];

        Assert.Equal(0.70, dominant.Ratio, precision: 2);
        Assert.Equal(0.30, minor.Ratio, precision: 2);
        Assert.Equal(0.70, minor.DominantRatio, precision: 2);
        Assert.Equal(0.40, minor.Gap, precision: 2);
        Assert.Equal(1, dominant.Rank);
        Assert.Equal(2, minor.Rank);
    }

    [Fact]
    public void Analyze_EmptyProfile_ReturnsEmptyReport()
    {
        var metadata = new RelationProfileMetadata(
            DateTime.UtcNow.ToString("O"), 0, 0, "empty");
        var profile = new RelationProfile(metadata, []);

        var report = DeviationAnalyzer.Analyze(profile);

        Assert.Empty(report.Analyses);
    }

    [Fact]
    public void Analyze_MultipleTablePairs_AnalyzedIndependently()
    {
        var rel1 = new TableRelation("dbo", "A", "dbo", "B",
            [new JoinPattern("INNER", [new ColumnPair("Id", "AId")], 50, ["f1.sql"])]);
        var rel2 = new TableRelation("dbo", "C", "dbo", "D",
            [new JoinPattern("LEFT", [new ColumnPair("Id", "CId")], 30, ["f2.sql"])]);
        var metadata = new RelationProfileMetadata(
            DateTime.UtcNow.ToString("O"), 2, 80, "test");
        var profile = new RelationProfile(metadata, [rel1, rel2]);

        var report = DeviationAnalyzer.Analyze(profile);

        Assert.Equal(2, report.Analyses.Count);
        Assert.Equal(50, report.Analyses[0].Total);
        Assert.Equal(30, report.Analyses[1].Total);
    }

    [Fact]
    public void Analyze_DifferentJoinType_DetectsStructuralDiff()
    {
        var profile = BuildProfile(
            ("INNER", 80, JoinShape.None, ["Id", "AId"]),
            ("LEFT", 20, JoinShape.None, ["Id", "AId"]));

        var report = DeviationAnalyzer.Analyze(profile);

        Assert.Contains(StructuralDiff.DifferentJoinType,
            report.Analyses[0].Deviations[1].StructuralDiffs);
    }
}
