using TsqlRefine.Schema.Relations;

namespace TsqlRefine.Schema.Tests.Relations;

public sealed class RelationAggregatorTests
{
    [Fact]
    public void Aggregate_DuplicateJoins_CountsOccurrences()
    {
        var rawJoins = new[]
        {
            new RawJoinInfo("dbo", "Users", "dbo", "Orders", "INNER", [new ColumnPair("Id", "UserId")], "file1.sql", JoinShape.None),
            new RawJoinInfo("dbo", "Users", "dbo", "Orders", "INNER", [new ColumnPair("Id", "UserId")], "file2.sql", JoinShape.None),
            new RawJoinInfo("dbo", "Users", "dbo", "Orders", "INNER", [new ColumnPair("Id", "UserId")], "file3.sql", JoinShape.None),
        };

        var profile = RelationAggregator.Aggregate(rawJoins, 3);

        Assert.Single(profile.Relations);
        Assert.Single(profile.Relations[0].Patterns);
        Assert.Equal(3, profile.Relations[0].Patterns[0].OccurrenceCount);
        Assert.Equal(3, profile.Relations[0].Patterns[0].SourceFiles.Count);
    }

    [Fact]
    public void Aggregate_ReversedTablePair_Canonicalizes()
    {
        var rawJoins = new[]
        {
            new RawJoinInfo("dbo", "Users", "dbo", "Orders", "INNER", [new ColumnPair("Id", "UserId")], "file1.sql", JoinShape.None),
            new RawJoinInfo("dbo", "Orders", "dbo", "Users", "INNER", [new ColumnPair("UserId", "Id")], "file2.sql", JoinShape.None),
        };

        var profile = RelationAggregator.Aggregate(rawJoins, 2);

        // Should be canonicalized to a single table pair (Orders < Users lexicographically)
        Assert.Single(profile.Relations);
        Assert.Equal("Orders", profile.Relations[0].LeftTable);
        Assert.Equal("Users", profile.Relations[0].RightTable);
        Assert.Single(profile.Relations[0].Patterns);
        Assert.Equal(2, profile.Relations[0].Patterns[0].OccurrenceCount);
    }

    [Fact]
    public void Aggregate_DifferentJoinTypes_SeparatePatterns()
    {
        var rawJoins = new[]
        {
            new RawJoinInfo("dbo", "Users", "dbo", "Orders", "INNER", [new ColumnPair("Id", "UserId")], "file1.sql", JoinShape.None),
            new RawJoinInfo("dbo", "Users", "dbo", "Orders", "LEFT", [new ColumnPair("Id", "UserId")], "file2.sql", JoinShape.None),
        };

        var profile = RelationAggregator.Aggregate(rawJoins, 2);

        Assert.Single(profile.Relations);
        Assert.Equal(2, profile.Relations[0].Patterns.Count);
    }

    [Fact]
    public void Aggregate_SwappedLeftRight_AlignsJoinDirection()
    {
        var rawJoins = new[]
        {
            new RawJoinInfo("dbo", "Users", "dbo", "Orders", "LEFT", [new ColumnPair("Id", "UserId")], "file1.sql", JoinShape.None),
            new RawJoinInfo("dbo", "Orders", "dbo", "Users", "RIGHT", [new ColumnPair("UserId", "Id")], "file2.sql", JoinShape.None),
        };

        var profile = RelationAggregator.Aggregate(rawJoins, 2);

        Assert.Single(profile.Relations);
        Assert.Single(profile.Relations[0].Patterns);
        Assert.Equal("RIGHT", profile.Relations[0].Patterns[0].JoinType);
        Assert.Equal(2, profile.Relations[0].Patterns[0].OccurrenceCount);
    }

    [Fact]
    public void Aggregate_MultipleFiles_AggregatesAcross()
    {
        var rawJoins = new[]
        {
            new RawJoinInfo("dbo", "A", "dbo", "B", "INNER", [new ColumnPair("Id", "AId")], "file1.sql", JoinShape.None),
            new RawJoinInfo("dbo", "C", "dbo", "D", "LEFT", [new ColumnPair("Id", "CId")], "file2.sql", JoinShape.None),
        };

        var profile = RelationAggregator.Aggregate(rawJoins, 2);

        Assert.Equal(2, profile.Relations.Count);
        Assert.Equal(2, profile.Metadata.FileCount);
        Assert.Equal(2, profile.Metadata.TotalJoinCount);
    }

    [Fact]
    public void Aggregate_EmptyInput_ReturnsEmptyProfile()
    {
        var profile = RelationAggregator.Aggregate([], 0);

        Assert.Empty(profile.Relations);
        Assert.Equal(0, profile.Metadata.FileCount);
        Assert.Equal(0, profile.Metadata.TotalJoinCount);
    }

    [Fact]
    public void Aggregate_SamePatternDifferentFiles_DeduplicatesSourceFiles()
    {
        var rawJoins = new[]
        {
            new RawJoinInfo("dbo", "A", "dbo", "B", "INNER", [new ColumnPair("Id", "AId")], "file1.sql", JoinShape.None),
            new RawJoinInfo("dbo", "A", "dbo", "B", "INNER", [new ColumnPair("Id", "AId")], "file1.sql", JoinShape.None),
        };

        var profile = RelationAggregator.Aggregate(rawJoins, 1);

        Assert.Single(profile.Relations);
        Assert.Equal(2, profile.Relations[0].Patterns[0].OccurrenceCount);
        // Same file should appear only once in source files (HashSet deduplication)
        Assert.Single(profile.Relations[0].Patterns[0].SourceFiles);
    }

    [Fact]
    public void Aggregate_MetadataHasContentHash()
    {
        var rawJoins = new[]
        {
            new RawJoinInfo("dbo", "A", "dbo", "B", "INNER", [new ColumnPair("Id", "AId")], "file1.sql", JoinShape.None),
        };

        var profile = RelationAggregator.Aggregate(rawJoins, 1);

        Assert.NotNull(profile.Metadata.ContentHash);
        Assert.NotEmpty(profile.Metadata.ContentHash);
    }

    [Fact]
    public void Aggregate_SameColumnsWithDifferentFlags_SeparatePatterns()
    {
        var rawJoins = new[]
        {
            new RawJoinInfo("dbo", "A", "dbo", "B", "INNER", [new ColumnPair("Id", "AId")], "f1.sql", JoinShape.None),
            new RawJoinInfo("dbo", "A", "dbo", "B", "INNER", [new ColumnPair("Id", "AId")], "f2.sql", JoinShape.ContainsOr),
        };

        var profile = RelationAggregator.Aggregate(rawJoins, 2);

        Assert.Single(profile.Relations);
        Assert.Equal(2, profile.Relations[0].Patterns.Count);
    }

    [Fact]
    public void Aggregate_PreservesShapeFlags()
    {
        var rawJoins = new[]
        {
            new RawJoinInfo("dbo", "A", "dbo", "B", "INNER", [new ColumnPair("Id", "AId")], "f1.sql",
                JoinShape.ContainsFunction | JoinShape.ContainsRange),
        };

        var profile = RelationAggregator.Aggregate(rawJoins, 1);

        Assert.Equal(
            JoinShape.ContainsFunction | JoinShape.ContainsRange,
            profile.Relations[0].Patterns[0].ShapeFlags);
    }
}
