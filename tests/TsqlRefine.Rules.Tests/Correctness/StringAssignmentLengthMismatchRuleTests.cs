using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;
using TsqlRefine.Schema.Resolution;
using TsqlRefine.Schema.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class StringAssignmentLengthMismatchRuleTests
{
    private readonly StringAssignmentLengthMismatchRule _rule = new();

    private static SchemaProvider CreateSchema() =>
        new(TestSchemaBuilder.Create()
            .AddTable("dbo", "Source", table => table
                .AddColumn("LongValue", "nvarchar", maxLength: 80)
                .AddColumn("ShortValue", "nvarchar", maxLength: 10)
                .AddColumn("Value", "nvarchar", maxLength: 80))
            .AddTable("dbo", "Target", table => table
                .AddColumn("Value", "nvarchar", maxLength: 20))
            .Build());

    [Theory]
    [InlineData("DECLARE @value varchar(4); SET @value = 'abcde';")]
    [InlineData("DECLARE @value varchar(4); SELECT @value = 'ab' + 'cde';")]
    [InlineData("DECLARE @value varchar(4); SET @value = COALESCE(CAST('x' AS varchar(8)), '');")]
    [InlineData("DECLARE @value varchar(4); SET @value = CONCAT('abc', 'de');")]
    [InlineData("DECLARE @value varchar(4); SET @value = CONCAT_WS('-', 'ab', 'cd');")]
    public void Analyze_StaticallyOversizedVariableAssignment_ReturnsDiagnostic(string sql)
    {
        var diagnostic = Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));

        Assert.Equal("string-assignment-length-mismatch", diagnostic.Code);
    }

    [Fact]
    public void Analyze_ProcedureParameterAssignment_ReturnsDiagnostic()
    {
        const string sql = "CREATE PROCEDURE dbo.p @value varchar(3) AS SET @value = 'long';";

        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_TableVariableInsert_ReturnsDiagnostic()
    {
        const string sql = "DECLARE @items TABLE (Value nvarchar(3)); INSERT @items (Value) VALUES (N'long');";

        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_TemporaryTableUpdateFromColumn_ReturnsDiagnostic()
    {
        const string sql = "CREATE TABLE #items (ShortValue varchar(3), LongValue varchar(8)); UPDATE #items SET ShortValue = LongValue;";

        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_SchemaAwareInsertSelect_ReturnsDiagnostic()
    {
        const string sql = "INSERT dbo.Target (Value) SELECT s.LongValue FROM dbo.Source AS s;";

        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql, CreateSchema())));
    }

    [Fact]
    public void Analyze_SchemaAwareUpdate_ReturnsDiagnostic()
    {
        const string sql = "UPDATE t SET t.Value = s.LongValue FROM dbo.Target AS t CROSS JOIN dbo.Source AS s;";

        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql, CreateSchema())));
    }

    [Fact]
    public void Analyze_UpdateTargetNotRepeatedInFrom_UsesTargetColumnCapacity()
    {
        const string sql = "UPDATE dbo.Target SET Value = N'12345678901' FROM dbo.Source AS s;";

        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql, CreateSchema())));
    }

    [Theory]
    [InlineData("DECLARE @value varchar(3); SET @value = STUFF('a', 1, 0, 'long');")]
    [InlineData("DECLARE @source varchar(100), @value varchar(20); SET @value = CAST(@source AS varchar);")]
    [InlineData("DECLARE @value varchar(3); SET @value = ISNULL(NULL, 'long');")]
    public void Analyze_InferredOversizedExpression_ReturnsDiagnostic(string sql)
    {
        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Theory]
    [InlineData("DECLARE @value varchar(5); SET @value = 'abcde';")]
    [InlineData("DECLARE @value varchar(max); SET @value = 'a' + 'very long value';")]
    [InlineData("DECLARE @value varchar(3); SET @value = CAST('long value' AS varchar(3));")]
    [InlineData("DECLARE @value varchar(3); SET @value = dbo.UnknownFunction('long value');")]
    [InlineData("DECLARE @value varchar(3); SET @value = NULL;")]
    [InlineData("DECLARE @value varchar(3); SET @value = LEFT('abcdefghij', 3);")]
    [InlineData("DECLARE @source varchar(100), @value varchar(3); SET @value = SUBSTRING(@source, 1, 3);")]
    public void Analyze_SafeOrUnknownAssignment_ReturnsEmpty(string sql)
    {
        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Analyze_InsertWithoutColumnList_ReturnsEmpty()
    {
        const string sql = "DECLARE @items TABLE (Value varchar(3)); INSERT @items VALUES ('long');";

        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
    }

    [Fact]
    public void Metadata_HasExpectedValues()
    {
        Assert.Equal("Correctness", _rule.Metadata.Category);
        Assert.Equal(RuleSeverity.Warning, _rule.Metadata.DefaultSeverity);
        Assert.False(_rule.Metadata.Fixable);
    }
}
