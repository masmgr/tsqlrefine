using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class InsertColumnNotInTableRuleTests
{
    private readonly InsertColumnNotInTableRule _rule = new();

    private static SchemaProvider CreateSchema() =>
        new(TestSchemaBuilder.Create()
            .AddTable("dbo", "Users", t => t
                .AddColumn("Id", "int")
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .AddColumn("Email", "nvarchar", maxLength: 200))
            .Build());

    [Theory]
    [InlineData("INSERT INTO dbo.Users (Id, Name, Email) VALUES (1, N'Test', N'test@example.com');")]
    [InlineData("INSERT INTO dbo.Users (Id) VALUES (1);")]
    [InlineData("INSERT INTO Users (Name) VALUES (N'Test');")]
    public void Analyze_ValidColumns_ReturnsNoDiagnostics(string sql)
    {
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InvalidColumn_ReturnsDiagnostic()
    {
        const string sql = "INSERT INTO dbo.Users (Id, Name, BadColumn) VALUES (1, N'Test', N'Bad');";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("insert-column-not-in-table", diagnostics[0].Code);
        Assert.Contains("BadColumn", diagnostics[0].Message);
        Assert.Contains("dbo.Users", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MultipleInvalidColumns_ReturnsMultipleDiagnostics()
    {
        const string sql = "INSERT INTO dbo.Users (Bad1, Bad2) VALUES (1, 2);";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("insert-column-not-in-table", d.Code));
    }

    [Fact]
    public void Analyze_NoColumnList_ReturnsNoDiagnostics()
    {
        const string sql = "INSERT INTO dbo.Users VALUES (1, N'Test', N'test@example.com');";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UnresolvedTable_SkipsDiagnostic()
    {
        const string sql = "INSERT INTO dbo.NonExistent (Id) VALUES (1);";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TempTable_SkipsValidation()
    {
        const string sql = "INSERT INTO #Temp (Id, Anything) VALUES (1, 2);";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoSchema_ReturnsNoDiagnostics()
    {
        const string sql = "INSERT INTO dbo.Users (BadColumn) VALUES (1);";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CaseInsensitiveColumn_ReturnsNoDiagnostics()
    {
        const string sql = "INSERT INTO dbo.Users (ID, NAME) VALUES (1, N'Test');";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}
