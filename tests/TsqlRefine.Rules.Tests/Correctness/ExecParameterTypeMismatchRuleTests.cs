using TsqlRefine.Rules.Rules.Correctness;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class ExecParameterTypeMismatchRuleTests
{
    private readonly ExecParameterTypeMismatchRule _rule = new();

    [Fact]
    public void Analyze_BigintArgumentForIntParameter_ReturnsDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext(
            "DECLARE @id bigint; EXEC dbo.TakeId @id;",
            "CREATE PROCEDURE dbo.TakeId @id int AS SELECT @id;");

        var diagnostic = Assert.Single(_rule.Analyze(context));

        Assert.Contains("bigint", diagnostic.Message);
        Assert.Contains("int", diagnostic.Message);
    }

    [Fact]
    public void Analyze_IntArgumentForBigintParameter_ReturnsNoDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext(
            "EXEC dbo.TakeId 1;",
            "CREATE PROCEDURE dbo.TakeId @id bigint AS SELECT @id;");

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_StringLiteralExceedsParameterLength_ReturnsDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext(
            "EXEC dbo.TakeCode 'too-long';",
            "CREATE PROCEDURE dbo.TakeCode @code varchar(4) AS SELECT @code;");

        Assert.Single(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_AnsiStringLiteralExceedsNvarcharCharacterLength_ReturnsDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext(
            "EXEC dbo.TakeCode 'abcdefghij';",
            "CREATE PROCEDURE dbo.TakeCode @code nvarchar(5) AS SELECT @code;");

        Assert.Single(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_AnsiStringLiteralFitsNvarcharCharacterLength_ReturnsNoDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext(
            "EXEC dbo.TakeCode 'abcde';",
            "CREATE PROCEDURE dbo.TakeCode @code nvarchar(5) AS SELECT @code;");

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_UnknownExpressionType_ReturnsNoDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext(
            "EXEC dbo.TakeId ABS(-1);",
            "CREATE PROCEDURE dbo.TakeId @id int AS SELECT @id;");

        Assert.Empty(_rule.Analyze(context));
    }

    [Fact]
    public void Analyze_MaxStringArgumentForBoundedParameter_ReturnsDiagnostic()
    {
        var context = ExecCatalogRuleTestHelper.CreateContext(
            "DECLARE @value nvarchar(max); EXEC dbo.TakeCode @value;",
            "CREATE PROCEDURE dbo.TakeCode @value nvarchar(100) AS SELECT @value;");

        Assert.Single(_rule.Analyze(context));
    }
}
