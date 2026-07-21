using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class UpdateGeneratedColumnRuleTests
{
    private readonly UpdateGeneratedColumnRule _rule = new();

    private static SchemaProvider CreateSchema() =>
        new(TestSchemaBuilder.Create()
            .AddTable("dbo", "Users", table => table
                .AddColumn("Id", "int", isIdentity: true)
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .AddColumn("DisplayName", "nvarchar", maxLength: 200, isComputed: true))
            .Build());

    [Theory]
    [InlineData("UPDATE dbo.Users SET Id = 2 WHERE Id = 1;", "identity")]
    [InlineData("UPDATE dbo.Users SET DisplayName = N'test' WHERE Id = 1;", "computed")]
    [InlineData("UPDATE u SET u.Id = 2 FROM dbo.Users AS u WHERE u.Id = 1;", "identity")]
    public void Analyze_GeneratedColumnWrite_ReturnsDiagnostic(string sql, string expectedKind)
    {
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostic = Assert.Single(_rule.Analyze(context));

        Assert.Equal("update-generated-column", diagnostic.Code);
        Assert.Contains(expectedKind, diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_OrdinaryColumn_ReturnsNoDiagnostic()
    {
        var context = RuleTestContext.CreateContext(
            "UPDATE dbo.Users SET Name = N'test' WHERE Id = 1;",
            CreateSchema());

        Assert.Empty(_rule.Analyze(context));
    }
}
