using TsqlRefine.Schema.Relations;

namespace TsqlRefine.Schema.Tests.Relations;

public sealed class RelationCollectorTests
{
    [Fact]
    public void Collect_MultipleFiles_ProducesCorrectProfile()
    {
        var inputs = new (string Sql, string FilePath)[]
        {
            ("SELECT * FROM dbo.Users u INNER JOIN dbo.Orders o ON u.Id = o.UserId", "file1.sql"),
            ("SELECT * FROM dbo.Users u LEFT JOIN dbo.Roles r ON u.RoleId = r.Id", "file2.sql"),
        };

        var profile = RelationCollector.Collect(inputs, 150);

        Assert.Equal(2, profile.Metadata.FileCount);
        Assert.Equal(2, profile.Metadata.TotalJoinCount);
        Assert.Equal(2, profile.Relations.Count);
    }

    [Fact]
    public void Collect_ParseError_SkipsGracefully()
    {
        var inputs = new (string Sql, string FilePath)[]
        {
            ("THIS IS NOT VALID SQL !!!", "bad.sql"),
            ("SELECT * FROM dbo.A a INNER JOIN dbo.B b ON a.Id = b.AId", "good.sql"),
        };

        var profile = RelationCollector.Collect(inputs, 150);

        // Should still produce results from the valid file
        // The invalid SQL may still parse partially, so just check it doesn't throw
        Assert.Equal(2, profile.Metadata.FileCount);
        Assert.True(profile.Metadata.TotalJoinCount >= 1);
    }

    [Fact]
    public void Collect_EmptyInputs_ReturnsEmptyProfile()
    {
        var profile = RelationCollector.Collect([], 150);

        Assert.Empty(profile.Relations);
        Assert.Equal(0, profile.Metadata.FileCount);
        Assert.Equal(0, profile.Metadata.TotalJoinCount);
    }

    [Fact]
    public void Collect_SameJoinAcrossFiles_AggregatesCorrectly()
    {
        var sql = "SELECT * FROM dbo.Users u INNER JOIN dbo.Orders o ON u.Id = o.UserId";
        var inputs = new (string Sql, string FilePath)[]
        {
            (sql, "file1.sql"),
            (sql, "file2.sql"),
            (sql, "file3.sql"),
        };

        var profile = RelationCollector.Collect(inputs, 150);

        Assert.Single(profile.Relations);
        Assert.Single(profile.Relations[0].Patterns);
        Assert.Equal(3, profile.Relations[0].Patterns[0].OccurrenceCount);
    }
}
