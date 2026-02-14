using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Schema;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Schema;

public sealed class AvoidDeprecatedTypesRuleTests
{
    private readonly AvoidDeprecatedTypesRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("avoid-deprecated-types", _rule.Metadata.RuleId);
        Assert.Equal("Schema", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    [Fact]
    public void Analyze_TextColumn_ReturnsDiagnostic()
    {
        const string sql = "CREATE TABLE dbo.Docs (Content TEXT NOT NULL);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-deprecated-types", diagnostics[0].Code);
        Assert.Contains("TEXT", diagnostics[0].Message);
        Assert.Contains("VARCHAR(MAX)", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_NtextColumn_ReturnsDiagnostic()
    {
        const string sql = "CREATE TABLE dbo.Docs (Content NTEXT NOT NULL);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-deprecated-types", diagnostics[0].Code);
        Assert.Contains("NTEXT", diagnostics[0].Message);
        Assert.Contains("NVARCHAR(MAX)", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_ImageColumn_ReturnsDiagnostic()
    {
        const string sql = "CREATE TABLE dbo.Docs (Photo IMAGE NOT NULL);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-deprecated-types", diagnostics[0].Code);
        Assert.Contains("IMAGE", diagnostics[0].Message);
        Assert.Contains("VARBINARY(MAX)", diagnostics[0].Message);
    }

    [Fact]
    public void Analyze_TextVariable_ReturnsDiagnostic()
    {
        const string sql = "DECLARE @v NTEXT;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-deprecated-types", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ImageParameter_ReturnsDiagnostic()
    {
        const string sql = "CREATE PROCEDURE dbo.SavePhoto @photo IMAGE AS SELECT 1;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-deprecated-types", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_MultipleDeprecatedColumns_ReturnsMultipleDiagnostics()
    {
        const string sql = @"
            CREATE TABLE dbo.Legacy (
                Description TEXT NOT NULL,
                Notes NTEXT NULL,
                Photo IMAGE NULL
            );
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-deprecated-types", d.Code));
    }

    [Fact]
    public void Analyze_TextInAlterTable_ReturnsDiagnostic()
    {
        const string sql = "ALTER TABLE dbo.Docs ADD Content TEXT;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-deprecated-types", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_CastToText_ReturnsDiagnostic()
    {
        const string sql = "SELECT CAST(Name AS TEXT) FROM dbo.Users;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("avoid-deprecated-types", diagnostics[0].Code);
    }

    [Theory]
    [InlineData("CREATE TABLE dbo.Docs (Content VARCHAR(MAX) NOT NULL);")]
    [InlineData("CREATE TABLE dbo.Docs (Content NVARCHAR(MAX) NOT NULL);")]
    [InlineData("CREATE TABLE dbo.Docs (Photo VARBINARY(MAX) NOT NULL);")]
    [InlineData("CREATE TABLE dbo.Docs (Name NVARCHAR(100) NOT NULL);")]
    [InlineData("CREATE TABLE dbo.Docs (Id INT NOT NULL);")]
    [InlineData("DECLARE @v NVARCHAR(MAX);")]
    [InlineData("DECLARE @amount DECIMAL(18,2);")]
    [InlineData("")]
    public void Analyze_NonDeprecatedType_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "CREATE TABLE dbo.Docs (Content TEXT NOT NULL);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
