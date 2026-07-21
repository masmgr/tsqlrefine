using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class InsertIntoGeneratedColumnRuleTests
{
    private readonly InsertIntoGeneratedColumnRule _rule = new();

    private static SchemaProvider CreateSchema() =>
        new(TestSchemaBuilder.Create()
            .AddTable("dbo", "Users", table => table
                .AddColumn("Id", "int", isIdentity: true)
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .AddColumn("DisplayName", "nvarchar", maxLength: 200, isComputed: true))
            .Build());

    [Theory]
    [InlineData("INSERT dbo.Users (DisplayName) VALUES (N'test');", "computed")]
    [InlineData("INSERT dbo.Users (Id, Name) VALUES (1, N'test');", "identity")]
    public void Analyze_GeneratedColumnWrite_ReturnsDiagnostic(string sql, string expectedKind)
    {
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostic = Assert.Single(_rule.Analyze(context));

        Assert.Equal("insert-into-generated-column", diagnostic.Code);
        Assert.Contains(expectedKind, diagnostic.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_IdentityInsertEnabled_ReturnsNoDiagnostic()
    {
        const string sql = """
            SET IDENTITY_INSERT dbo.Users ON;
            INSERT dbo.Users (Id, Name) VALUES (1, N'test');
            SET IDENTITY_INSERT dbo.Users OFF;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_OrdinaryColumn_ReturnsNoDiagnostic()
    {
        var context = RuleTestContext.CreateContext(
            "INSERT dbo.Users (Name) VALUES (N'test');",
            CreateSchema());

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_IdentityInsertState_DoesNotLeakBetweenProcedures()
    {
        const string sql = """
            CREATE PROCEDURE dbo.LoadWithIdentity
            AS
            BEGIN
                SET IDENTITY_INSERT dbo.Users ON;
                INSERT dbo.Users (Id, Name) VALUES (1, N'first');
            END;
            GO
            CREATE PROCEDURE dbo.LoadWithoutIdentity
            AS
            BEGIN
                INSERT dbo.Users (Id, Name) VALUES (2, N'second');
            END;
            """;
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        Assert.Single(_rule.Analyze(context));
    }
}
