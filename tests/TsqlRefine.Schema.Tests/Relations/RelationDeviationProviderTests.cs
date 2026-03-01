using TsqlRefine.PluginSdk;
using TsqlRefine.Schema.Relations;

namespace TsqlRefine.Schema.Tests.Relations;

public sealed class RelationDeviationProviderTests
{
    private static RelationProfile BuildProfile(
        params (string LeftSchema, string LeftTable, string RightSchema, string RightTable,
            string JoinType, int Count, JoinShape Flags, string[] Cols)[] entries)
    {
        var relations = new List<TableRelation>();
        var totalJoins = 0;

        foreach (var group in entries.GroupBy(e => $"{e.LeftSchema}.{e.LeftTable}|{e.RightSchema}.{e.RightTable}"))
        {
            var first = group.First();
            var patterns = group.Select((e, i) =>
                new JoinPattern(
                    e.JoinType,
                    Enumerable.Range(0, e.Cols.Length / 2)
                        .Select(j => new ColumnPair(e.Cols[j * 2], e.Cols[j * 2 + 1]))
                        .ToList(),
                    e.Count,
                    [$"file{i}.sql"],
                    e.Flags))
                .OrderByDescending(p => p.OccurrenceCount)
                .ToList();

            relations.Add(new TableRelation(
                first.LeftSchema, first.LeftTable, first.RightSchema, first.RightTable, patterns));
            totalJoins += patterns.Sum(p => p.OccurrenceCount);
        }

        var metadata = new RelationProfileMetadata(
            DateTime.UtcNow.ToString("O"), 1, totalJoins, "test");
        return new RelationProfile(metadata, relations);
    }

    [Fact]
    public void FromProfile_HasData_ReturnsTrue()
    {
        var profile = BuildProfile(
            ("dbo", "A", "dbo", "B", "INNER", 50, JoinShape.None, ["Id", "AId"]));

        var provider = RelationDeviationProvider.FromProfile(profile);

        Assert.True(provider.HasData);
        Assert.Equal(1, provider.TablePairCount);
    }

    [Fact]
    public void FromProfile_EmptyProfile_HasDataFalse()
    {
        var metadata = new RelationProfileMetadata(
            DateTime.UtcNow.ToString("O"), 0, 0, "empty");
        var profile = new RelationProfile(metadata, []);

        var provider = RelationDeviationProvider.FromProfile(profile);

        Assert.False(provider.HasData);
        Assert.Equal(0, provider.TablePairCount);
    }

    [Fact]
    public void GetTablePairSummary_ExistingPair_ReturnsSummary()
    {
        var profile = BuildProfile(
            ("dbo", "A", "dbo", "B", "INNER", 80, JoinShape.None, ["Id", "AId"]),
            ("dbo", "A", "dbo", "B", "LEFT", 20, JoinShape.None, ["Id", "AId"]));

        var provider = RelationDeviationProvider.FromProfile(profile);
        var summary = provider.GetTablePairSummary("dbo", "A", "dbo", "B");

        Assert.NotNull(summary);
        Assert.Equal("dbo", summary!.LeftSchema);
        Assert.Equal("A", summary.LeftTable);
        Assert.Equal("dbo", summary.RightSchema);
        Assert.Equal("B", summary.RightTable);
        Assert.Equal(100, summary.Total);
        Assert.Equal(2, summary.PatternCount);
        Assert.Equal(2, summary.Deviations.Count);
    }

    [Fact]
    public void GetTablePairSummary_ReversedOrder_StillReturns()
    {
        // Table pair is stored as A|B (lexicographic), query with B|A should still work
        var profile = BuildProfile(
            ("dbo", "A", "dbo", "B", "INNER", 50, JoinShape.None, ["Id", "AId"]));

        var provider = RelationDeviationProvider.FromProfile(profile);

        // Query in reverse order
        var summary = provider.GetTablePairSummary("dbo", "B", "dbo", "A");

        Assert.NotNull(summary);
        Assert.Equal("A", summary!.LeftTable);
        Assert.Equal("B", summary.RightTable);
    }

    [Fact]
    public void GetTablePairSummary_CaseInsensitiveLookup_ReturnsSummary()
    {
        var profile = BuildProfile(
            ("dbo", "A", "dbo", "B", "INNER", 50, JoinShape.None, ["Id", "AId"]));

        var provider = RelationDeviationProvider.FromProfile(profile);
        var summary = provider.GetTablePairSummary("DBO", "a", "dbo", "b");

        Assert.NotNull(summary);
        Assert.Equal("A", summary!.LeftTable);
        Assert.Equal("B", summary.RightTable);
    }

    [Fact]
    public void GetTablePairSummary_NonExistent_ReturnsNull()
    {
        var profile = BuildProfile(
            ("dbo", "A", "dbo", "B", "INNER", 50, JoinShape.None, ["Id", "AId"]));

        var provider = RelationDeviationProvider.FromProfile(profile);
        var summary = provider.GetTablePairSummary("dbo", "X", "dbo", "Y");

        Assert.Null(summary);
    }

    [Fact]
    public void GetAllSummaries_ReturnsAll()
    {
        var profile = BuildProfile(
            ("dbo", "A", "dbo", "B", "INNER", 50, JoinShape.None, ["Id", "AId"]),
            ("dbo", "C", "dbo", "D", "LEFT", 30, JoinShape.None, ["Id", "CId"]));

        var provider = RelationDeviationProvider.FromProfile(profile);
        var summaries = provider.GetAllSummaries();

        Assert.Equal(2, summaries.Count);
    }

    [Fact]
    public void Deviations_ConvertsLevelsCorrectly()
    {
        // 80 + 20 = 100; dominant + structural (different join type)
        var profile = BuildProfile(
            ("dbo", "A", "dbo", "B", "INNER", 80, JoinShape.None, ["Id", "AId"]),
            ("dbo", "A", "dbo", "B", "LEFT", 20, JoinShape.None, ["Id", "AId"]));

        var provider = RelationDeviationProvider.FromProfile(profile);
        var summary = provider.GetTablePairSummary("dbo", "A", "dbo", "B");

        Assert.NotNull(summary);
        Assert.Equal(RelationDeviationLevel.Dominant, summary!.Deviations[0].Level);
        // LEFT vs INNER = structural diff
        Assert.Equal(RelationDeviationLevel.Structural, summary.Deviations[1].Level);
        Assert.Contains(RelationStructuralDiff.DifferentJoinType, summary.Deviations[1].StructuralDiffs);
    }

    [Fact]
    public void Deviations_ColumnPairDescriptions_Correct()
    {
        var profile = BuildProfile(
            ("dbo", "A", "dbo", "B", "INNER", 50, JoinShape.None, ["Id", "AId", "Code", "Code"]));

        var provider = RelationDeviationProvider.FromProfile(profile);
        var summary = provider.GetTablePairSummary("dbo", "A", "dbo", "B");

        Assert.NotNull(summary);
        var dev = summary!.Deviations[0];
        Assert.Equal("INNER", dev.JoinType);
        Assert.Equal(50, dev.OccurrenceCount);
        Assert.Equal(2, dev.ColumnPairDescriptions.Count);
        Assert.Contains("Id=AId", dev.ColumnPairDescriptions);
        Assert.Contains("Code=Code", dev.ColumnPairDescriptions);
    }

    [Fact]
    public void Deviations_RatioAndGap_Correct()
    {
        var profile = BuildProfile(
            ("dbo", "A", "dbo", "B", "INNER", 70, JoinShape.None, ["Id", "AId"]),
            ("dbo", "A", "dbo", "B", "INNER", 30, JoinShape.None, ["Code", "Code"]));

        var provider = RelationDeviationProvider.FromProfile(profile);
        var summary = provider.GetTablePairSummary("dbo", "A", "dbo", "B");

        Assert.NotNull(summary);
        Assert.Equal(0.70, summary!.Deviations[0].Ratio, precision: 2);
        Assert.Equal(0.30, summary.Deviations[1].Ratio, precision: 2);
        Assert.Equal(0.40, summary.Deviations[1].Gap, precision: 2);
        Assert.Equal(1, summary.Deviations[0].Rank);
        Assert.Equal(2, summary.Deviations[1].Rank);
    }
}
