using TsqlRefine.Schema.Relations;

namespace TsqlRefine.Schema.Tests.Relations;

public sealed class RelationProfileSerializerTests
{
    private static RelationProfile CreateSampleProfile()
    {
        var relations = new List<TableRelation>
        {
            new(
                "dbo", "Users", "dbo", "Orders",
                [
                    new JoinPattern(
                        "INNER",
                        [new ColumnPair("Id", "UserId")],
                        3,
                        ["file1.sql", "file2.sql"])
                ])
        };

        var hash = RelationProfileSerializer.ComputeContentHash(relations);
        var metadata = new RelationProfileMetadata(
            GeneratedAt: "2026-01-01T00:00:00Z",
            FileCount: 2,
            TotalJoinCount: 3,
            ContentHash: hash
        );

        return new RelationProfile(metadata, relations);
    }

    [Fact]
    public void RoundTrip_Preserves()
    {
        var original = CreateSampleProfile();
        var json = RelationProfileSerializer.Serialize(original);
        var deserialized = RelationProfileSerializer.Deserialize(json);

        Assert.Equal(original.Metadata.GeneratedAt, deserialized.Metadata.GeneratedAt);
        Assert.Equal(original.Metadata.FileCount, deserialized.Metadata.FileCount);
        Assert.Equal(original.Metadata.TotalJoinCount, deserialized.Metadata.TotalJoinCount);
        Assert.Equal(original.Metadata.ContentHash, deserialized.Metadata.ContentHash);
        Assert.Single(deserialized.Relations);
        Assert.Equal("dbo", deserialized.Relations[0].LeftSchema);
        Assert.Equal("Users", deserialized.Relations[0].LeftTable);
        Assert.Equal("dbo", deserialized.Relations[0].RightSchema);
        Assert.Equal("Orders", deserialized.Relations[0].RightTable);
        Assert.Single(deserialized.Relations[0].Patterns);
        Assert.Equal("INNER", deserialized.Relations[0].Patterns[0].JoinType);
        Assert.Equal(3, deserialized.Relations[0].Patterns[0].OccurrenceCount);
        Assert.Single(deserialized.Relations[0].Patterns[0].ColumnPairs);
        Assert.Equal("Id", deserialized.Relations[0].Patterns[0].ColumnPairs[0].LeftColumn);
        Assert.Equal("UserId", deserialized.Relations[0].Patterns[0].ColumnPairs[0].RightColumn);
    }

    [Fact]
    public void Serialize_ProducesCamelCaseJson()
    {
        var profile = CreateSampleProfile();
        var json = RelationProfileSerializer.Serialize(profile);

        Assert.Contains("\"metadata\"", json);
        Assert.Contains("\"relations\"", json);
        Assert.Contains("\"generatedAt\"", json);
        Assert.Contains("\"fileCount\"", json);
        Assert.Contains("\"totalJoinCount\"", json);
        Assert.Contains("\"contentHash\"", json);
        Assert.Contains("\"leftSchema\"", json);
        Assert.Contains("\"leftTable\"", json);
        Assert.Contains("\"joinType\"", json);
        Assert.Contains("\"columnPairs\"", json);
        Assert.Contains("\"occurrenceCount\"", json);
        Assert.Contains("\"sourceFiles\"", json);
    }

    [Fact]
    public void ComputeContentHash_SameContent_SameHash()
    {
        var relations1 = new List<TableRelation>
        {
            new("dbo", "A", "dbo", "B", [new JoinPattern("INNER", [new ColumnPair("Id", "AId")], 1, ["f.sql"])])
        };
        var relations2 = new List<TableRelation>
        {
            new("dbo", "A", "dbo", "B", [new JoinPattern("INNER", [new ColumnPair("Id", "AId")], 1, ["f.sql"])])
        };

        var hash1 = RelationProfileSerializer.ComputeContentHash(relations1);
        var hash2 = RelationProfileSerializer.ComputeContentHash(relations2);

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeContentHash_DifferentContent_DifferentHash()
    {
        var relations1 = new List<TableRelation>
        {
            new("dbo", "A", "dbo", "B", [new JoinPattern("INNER", [new ColumnPair("Id", "AId")], 1, ["f.sql"])])
        };
        var relations2 = new List<TableRelation>
        {
            new("dbo", "A", "dbo", "C", [new JoinPattern("INNER", [new ColumnPair("Id", "AId")], 1, ["f.sql"])])
        };

        var hash1 = RelationProfileSerializer.ComputeContentHash(relations1);
        var hash2 = RelationProfileSerializer.ComputeContentHash(relations2);

        Assert.NotEqual(hash1, hash2);
    }
}
