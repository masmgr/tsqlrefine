using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class UpdateColumnNotInTableRuleTests
{
    private readonly UpdateColumnNotInTableRule _rule = new();

    private static SchemaProvider CreateSchema() =>
        new(TestSchemaBuilder.Create()
            .AddTable("dbo", "Users", t => t
                .AddColumn("Id", "int")
                .AddColumn("Name", "nvarchar", maxLength: 100)
                .AddColumn("Email", "nvarchar", maxLength: 200))
            .Build());

    [Theory]
    [InlineData("UPDATE dbo.Users SET Name = N'Test' WHERE Id = 1;")]
    [InlineData("UPDATE dbo.Users SET Name = N'Test', Email = N'a@b.com' WHERE Id = 1;")]
    [InlineData("UPDATE Users SET Id = 2 WHERE Id = 1;")]
    public void Analyze_ValidColumns_ReturnsNoDiagnostics(string sql)
    {
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_InvalidColumn_ReturnsDiagnostic()
    {
        const string sql = "UPDATE dbo.Users SET BadColumn = 1 WHERE Id = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("update-column-not-in-table", diagnostics[0].Code);
        Assert.Contains("BadColumn", diagnostics[0].Message);
        Assert.Contains("dbo.Users", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_MultipleInvalidColumns_ReturnsMultipleDiagnostics()
    {
        const string sql = "UPDATE dbo.Users SET Bad1 = 1, Bad2 = 2 WHERE Id = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("update-column-not-in-table", d.Code));
    }

    [Fact]
    public void Analyze_MixedValidAndInvalid_ReportOnlyInvalid()
    {
        const string sql = "UPDATE dbo.Users SET Name = N'Test', BadColumn = 1 WHERE Id = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Contains("BadColumn", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_UnresolvedTable_SkipsDiagnostic()
    {
        const string sql = "UPDATE dbo.NonExistent SET Col1 = 1 WHERE Id = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_TempTable_SkipsValidation()
    {
        const string sql = "UPDATE #Temp SET Anything = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NoSchema_ReturnsNoDiagnostics()
    {
        const string sql = "UPDATE dbo.Users SET BadColumn = 1 WHERE Id = 1;";
        var context = RuleTestContext.CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_CaseInsensitiveColumn_ReturnsNoDiagnostics()
    {
        const string sql = "UPDATE dbo.Users SET NAME = N'Test' WHERE ID = 1;";
        var context = RuleTestContext.CreateContext(sql, CreateSchema());

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }
}
