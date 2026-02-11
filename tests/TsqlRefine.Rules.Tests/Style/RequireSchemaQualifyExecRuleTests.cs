using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class RequireSchemaQualifyExecRuleTests
{
    private readonly RequireSchemaQualifyExecRule _rule = new();

    [Fact]
    public void Analyze_ExecWithoutSchema_ReturnsDiagnostic()
    {
        const string sql = "EXEC MyProc;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal("require-schema-qualify-exec", diagnostic.Code);
        Assert.Contains("MyProc", diagnostic.Message);
    }

    [Fact]
    public void Analyze_ExecuteWithoutSchema_ReturnsDiagnostic()
    {
        const string sql = "EXECUTE MyProc;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("MyProc", diagnostic.Message);
    }

    [Fact]
    public void Analyze_ExecWithSchema_ReturnsNoDiagnostic()
    {
        const string sql = "EXEC dbo.MyProc;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExecWithSchemaAndParams_ReturnsNoDiagnostic()
    {
        const string sql = "EXEC dbo.GetUserById @UserId = 1;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExecSystemProc_ReturnsNoDiagnostic()
    {
        const string sql = "EXEC sp_executesql @sql;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Theory]
    [InlineData("EXEC sp_xml_preparedocument @handle OUTPUT, @xml;")]
    [InlineData("EXEC sp_xml_removedocument @handle;")]
    [InlineData("EXEC sp_getapplock @Resource = 'MyResource', @LockMode = 'Exclusive';")]
    [InlineData("EXEC sp_releaseapplock @Resource = 'MyResource';")]
    [InlineData("EXEC sp_describe_first_result_set @sql;")]
    public void Analyze_KnownSystemProcs_ReturnsNoDiagnostic(string sql)
    {
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExecTempProc_ReturnsNoDiagnostic()
    {
        const string sql = "EXEC #TempProc;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExecGlobalTempProc_ReturnsNoDiagnostic()
    {
        const string sql = "EXEC ##GlobalTempProc;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExecVariable_ReturnsNoDiagnostic()
    {
        const string sql = "DECLARE @proc NVARCHAR(100) = N'dbo.MyProc';\nEXEC @proc;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_UserDefinedSpProcWithoutSchema_ReturnsDiagnostic()
    {
        const string sql = "EXEC sp_MyCustomProc;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        var diagnostic = Assert.Single(diagnostics);
        Assert.Contains("sp_MyCustomProc", diagnostic.Message);
    }

    [Fact]
    public void Analyze_UserDefinedSpProcWithSchema_ReturnsNoDiagnostic()
    {
        const string sql = "EXEC dbo.sp_MyCustomProc;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_ExecWithDatabaseAndSchema_ReturnsNoDiagnostic()
    {
        const string sql = "EXEC MyDatabase.dbo.MyProc;";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_EmptyInput_ReturnsNoDiagnostic()
    {
        const string sql = "";
        var context = CreateContext(sql);

        var diagnostics = _rule.Analyze(context).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Metadata_HasCorrectProperties()
    {
        Assert.Equal("require-schema-qualify-exec", _rule.Metadata.RuleId);
        Assert.Equal("Style", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }

    private static RuleContext CreateContext(string sql)
    {
        return RuleTestContext.CreateContext(sql);
    }
}
