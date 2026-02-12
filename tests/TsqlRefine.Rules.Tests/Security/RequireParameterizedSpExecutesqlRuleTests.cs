using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Security;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Security;

public sealed class RequireParameterizedSpExecutesqlRuleTests
{
    private readonly RequireParameterizedSpExecutesqlRule _rule = new();

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("require-parameterized-sp-executesql", _rule.Metadata.RuleId);
        Assert.Equal("Security", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    // === Positive: no parameter definitions ===

    [Fact]
    public void Analyze_SpExecutesqlWithoutParameterDefinitions_ReturnsDiagnostic()
    {
        const string sql = "EXEC sp_executesql N'SELECT * FROM dbo.Users WHERE Id = 1';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-parameterized-sp-executesql", diagnostics[0].Code);
        Assert.Contains("parameter", diagnostics[0].Message.ToLowerInvariant());
        // Diagnostic should highlight only the "sp_executesql" procedure name
        Assert.Equal(0, diagnostics[0].Range.Start.Line);
        Assert.Equal(5, diagnostics[0].Range.Start.Character);
        Assert.Equal(0, diagnostics[0].Range.End.Line);
        Assert.Equal(18, diagnostics[0].Range.End.Character);
    }

    [Fact]
    public void Analyze_SpExecutesqlWithVariableNoParams_ReturnsDiagnostic()
    {
        const string sql = "EXEC sp_executesql @sql;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-parameterized-sp-executesql", diagnostics[0].Code);
        // Diagnostic should highlight only the "sp_executesql" procedure name
        Assert.Equal(0, diagnostics[0].Range.Start.Line);
        Assert.Equal(5, diagnostics[0].Range.Start.Character);
        Assert.Equal(0, diagnostics[0].Range.End.Line);
        Assert.Equal(18, diagnostics[0].Range.End.Character);
    }

    [Fact]
    public void Analyze_SpExecutesqlCaseInsensitive_ReturnsDiagnostic()
    {
        const string sql = "EXEC SP_EXECUTESQL N'SELECT 1';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Single(diagnostics);
        Assert.Equal("require-parameterized-sp-executesql", diagnostics[0].Code);
    }

    // === Negative: properly parameterized ===

    [Fact]
    public void Analyze_SpExecutesqlWithParameters_NoDiagnostic()
    {
        const string sql = "EXEC sp_executesql N'SELECT * FROM dbo.Users WHERE Id = @id', N'@id INT', @id = @userId;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_SpExecutesqlWithMultipleParameters_NoDiagnostic()
    {
        const string sql = @"
            EXEC sp_executesql
                N'SELECT * FROM dbo.Users WHERE Id = @id AND Name = @name',
                N'@id INT, @name NVARCHAR(100)',
                @id = 1,
                @name = N'test';
        ";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    // === Negative: not sp_executesql ===

    [Fact]
    public void Analyze_ExecDynamicSql_NoDiagnostic()
    {
        const string sql = "EXEC(@sql);";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExecStoredProcedure_NoDiagnostic()
    {
        const string sql = "EXEC dbo.MyProcedure @param1 = 1;";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("SELECT * FROM dbo.Users;")]
    [InlineData("EXEC dbo.SomeProc;")]
    [InlineData("")]
    public void Analyze_NonSpExecutesql_NoDiagnostic(string sql)
    {
        var context = RuleTestContext.CreateContext(sql);
        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        const string sql = "EXEC sp_executesql N'SELECT 1';";
        var context = RuleTestContext.CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        var fixes = _rule.GetFixes(context, diagnostic);

        Assert.Empty(fixes);
    }
}
