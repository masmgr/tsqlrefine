using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class ExecParameterFunctionCallRuleTests
{
    private readonly ExecParameterFunctionCallRule _rule = new();

    [Fact]
    public void Analyze_NamedFunctionArgument_ReturnsDiagnosticForFunctionRange()
    {
        const string sql = "EXECUTE Proc1 @date = GETDATE();";

        var diagnostic = Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));

        Assert.Equal("exec-parameter-function-call", diagnostic.Code);
        Assert.Equal(RuleSeverity.Error, _rule.Metadata.DefaultSeverity);
        Assert.Equal(sql.IndexOf("GETDATE()", StringComparison.Ordinal), diagnostic.Range.Start.Character);
        Assert.Equal("GETDATE()".Length, diagnostic.Range.End.Character - diagnostic.Range.Start.Character);
    }

    [Fact]
    public void Analyze_PositionalFunctionArgument_ReturnsDiagnostic()
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext("EXEC Proc1 GETDATE();")).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_MultipleFunctionArguments_ReturnsDiagnosticForEachArgument()
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(
            "EXEC Proc1 @date = GETDATE(), @id = ABS(-1);")).ToArray();

        Assert.Equal(2, diagnostics.Length);
    }

    [Fact]
    public void Analyze_ReturnStatusAssignmentWithFunctionArgument_ReturnsDiagnostic()
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(
            "EXEC @returnCode = dbo.Proc1 @date = GETDATE();")).ToArray();

        Assert.Single(diagnostics);
    }

    [Fact]
    public void Analyze_DatabaseQualifiedProcedureWithOmittedSchema_ReturnsDiagnostic()
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(
            "EXEC Database1..Proc1 @date = GETDATE();")).ToArray();

        Assert.Single(diagnostics);
    }

    [Theory]
    [InlineData("CAST(1 AS int)")]
    [InlineData("CONVERT(int, 1)")]
    [InlineData("TRY_CONVERT(int, 1)")]
    [InlineData("PARSE('1' AS int)")]
    [InlineData("IIF(1 = 1, 1, 0)")]
    [InlineData("LEFT('abc', 1)")]
    [InlineData("RIGHT('abc', 1)")]
    [InlineData("dbo.GetValue()")]
    public void Analyze_FunctionForms_ReturnsDiagnostic(string expression)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(
            $"EXEC Proc1 @value = {expression};")).ToArray();

        Assert.Single(diagnostics);
    }

    [Theory]
    [InlineData("EXEC Proc1 @value = 1;")]
    [InlineData("DECLARE @value int = 1; EXEC Proc1 @value = @value;")]
    [InlineData("EXEC Proc1 @value = DEFAULT;")]
    [InlineData("DECLARE @value datetime = GETDATE(); EXEC Proc1 @value = @value;")]
    public void Analyze_NonDirectFunctionArgument_ReturnsNoDiagnostic(string sql)
    {
        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Theory]
    [InlineData("EXEC dbo.Proc1 1\nSELECT a, GETDATE();")]
    [InlineData("EXEC dbo.Proc1 1\nGO\nSELECT a, GETDATE();")]
    public void Analyze_FunctionInFollowingStatement_ReturnsNoDiagnostic(string sql)
    {
        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }
}
