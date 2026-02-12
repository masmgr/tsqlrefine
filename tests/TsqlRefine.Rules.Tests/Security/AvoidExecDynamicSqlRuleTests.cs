using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Security;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Security;

public sealed class AvoidExecDynamicSqlRuleTests
{
    private readonly AvoidExecDynamicSqlRule _rule = new();

    [Fact]
    public void Analyze_ExecWithVariable_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "EXEC(@sql);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        var diagnostic = diagnostics[0];
        Assert.Equal("avoid-exec-dynamic-sql", diagnostic.Code);
        Assert.Contains("dynamic SQL", diagnostic.Message);
        // Diagnostic should highlight only the EXEC keyword
        Assert.Equal(0, diagnostic.Range.Start.Line);
        Assert.Equal(0, diagnostic.Range.Start.Character);
        Assert.Equal(0, diagnostic.Range.End.Line);
        Assert.Equal(4, diagnostic.Range.End.Character);
    }

    [Fact]
    public void Analyze_ExecuteWithVariable_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "EXECUTE(@dynamicQuery);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-exec-dynamic-sql", diagnostics[0].Code);
        // Diagnostic should highlight only the EXECUTE keyword
        Assert.Equal(0, diagnostics[0].Range.Start.Line);
        Assert.Equal(0, diagnostics[0].Range.Start.Character);
        Assert.Equal(0, diagnostics[0].Range.End.Line);
        Assert.Equal(7, diagnostics[0].Range.End.Character);
    }

    [Fact]
    public void Analyze_ExecWithStringLiteral_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "EXEC('SELECT * FROM users');";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-exec-dynamic-sql", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ExecWithConcatenatedVariables_ReturnsDiagnostic()
    {
        // Arrange
        const string sql = "EXEC(@part1 + @part2);";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Single(diagnostics);
        Assert.Equal("avoid-exec-dynamic-sql", diagnostics[0].Code);
    }

    [Fact]
    public void Analyze_ExecStoredProcedure_NoDiagnostic()
    {
        // Arrange
        const string sql = "EXEC dbo.GetUsers;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExecStoredProcedureWithParameters_NoDiagnostic()
    {
        // Arrange
        const string sql = "EXEC MyStoredProc @id = 1, @name = 'test';";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExecuteSpExecutesql_NoDiagnostic()
    {
        // Arrange
        const string sql = "EXECUTE sp_executesql @stmt;";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExecWithParenthesesButProcedureName_NoDiagnostic()
    {
        // Arrange
        const string sql = "EXEC dbo.GetUsers();";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleExecStatements_ReturnsMultipleDiagnostics()
    {
        // Arrange
        const string sql = @"
            EXEC(@sql1);
            EXEC dbo.ValidProc;
            EXEC('SELECT 1');
        ";
        var context = CreateContext(sql);

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d => Assert.Equal("avoid-exec-dynamic-sql", d.Code));
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsEmpty()
    {
        // Arrange
        var context = CreateContext("");

        // Act
        var diagnostics = _rule.Analyze(context).ToArray();

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public void GetFixes_ReturnsEmptyCollection()
    {
        // Arrange
        const string sql = "EXEC(@sql);";
        var context = CreateContext(sql);
        var diagnostic = _rule.Analyze(context).First();

        // Act
        var fixes = _rule.GetFixes(context, diagnostic);

        // Assert
        Assert.Empty(fixes);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        // Assert
        Assert.Equal("avoid-exec-dynamic-sql", _rule.Metadata.RuleId);
        Assert.Equal("Security", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
