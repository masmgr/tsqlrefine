using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class InsertMissingRequiredColumnRuleTests
{
    private readonly InsertMissingRequiredColumnRule _rule = new();

    private static SchemaProvider CreateSchema() =>
        new(TestSchemaBuilder.Create()
            .AddTable("dbo", "Users", table => table
                .AddColumn("Id", "int", isIdentity: true)
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .AddColumn("Email", "nvarchar", nullable: true, maxLength: 200)
                .AddColumn("Status", "int", defaultExpression: "((0))")
                .AddColumn("DisplayName", "nvarchar", maxLength: 200, isComputed: true)
                .AddColumn("Version", "rowversion"))
            .AddView("dbo", "UserView", view => view
                .AddColumn("Name", "nvarchar", maxLength: 100))
            .Build());

    [Fact]
    public void Analyze_RequiredColumnOmitted_ReturnsDiagnostic()
    {
        var context = RuleTestContext.CreateContext(
            "INSERT dbo.Users (Email) VALUES (N'a@b.com');",
            CreateSchema());

        var diagnostic = Assert.Single(_rule.Analyze(context));

        Assert.Equal("insert-missing-required-column", diagnostic.Code);
        Assert.Contains("Name", diagnostic.Message);
        Assert.DoesNotContain("Status", diagnostic.Message);
        Assert.DoesNotContain("DisplayName", diagnostic.Message);
        Assert.DoesNotContain("Version", diagnostic.Message);
    }

    [Fact]
    public void Analyze_DefaultValuesWithRequiredColumn_ReturnsDiagnostic()
    {
        var context = RuleTestContext.CreateContext(
            "INSERT dbo.Users DEFAULT VALUES;",
            CreateSchema());

        Assert.Single(_rule.Analyze(context));
    }

    [Theory]
    [InlineData("INSERT dbo.Users (Name) VALUES (N'test');")]
    [InlineData("INSERT dbo.Users VALUES (N'test', NULL, 0);")]
    [InlineData("INSERT dbo.UserView DEFAULT VALUES;")]
    public void Analyze_ValidOrUnsupportedInsert_ReturnsNoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_UnresolvedListedColumn_SkipsCascadingDiagnostic()
    {
        var context = RuleTestContext.CreateContext(
            "INSERT dbo.Users (Nmae) VALUES (N'test');",
            CreateSchema());

        Assert.Empty(_rule.Analyze(context));
    }
}
