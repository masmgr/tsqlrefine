using TsqlRefine.Rules.Rules.Correctness;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Correctness;

public sealed class MixedStringLengthFunctionsInLoopRuleTests
{
    private readonly MixedStringLengthFunctionsInLoopRule _rule = new();

    [Theory]
    [InlineData("""
        WHILE DATALENGTH(@text) > 0
        BEGIN
            SET @text = SUBSTRING(@text, LEN(@chunk) + 1, LEN(@text));
        END
        """)]
    [InlineData("""
        WHILE DATALENGTH(CONVERT(varchar(max), @text)) > 0
        BEGIN
            SELECT @text = RIGHT(@text, LEN(@text) - 1);
        END
        """)]
    public void Analyze_MixedLengthSemanticsForSameLoopVariable_ReturnsDiagnostic(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, diagnostic =>
            Assert.Equal("mixed-string-length-functions-in-loop", diagnostic.Code));
    }

    [Theory]
    [InlineData("""
        WHILE DATALENGTH(@text) > 0
        BEGIN
            SET @text = SUBSTRING(@text, DATALENGTH(@chunk) + 1, DATALENGTH(@text));
        END
        """)]
    [InlineData("""
        WHILE LEN(@text) > 0
        BEGIN
            SET @text = SUBSTRING(@text, LEN(@chunk) + 1, LEN(@text));
        END
        """)]
    [InlineData("""
        WHILE DATALENGTH(@text) > 0
        BEGIN
            SET @other = SUBSTRING(@other, LEN(@chunk) + 1, LEN(@other));
        END
        """)]
    [InlineData("WHILE DATALENGTH(@text) > 0 SET @text = @text + 'x';")]
    public void Analyze_ConsistentOrUnrelatedPatterns_ReturnsEmpty(string sql)
    {
        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void Analyze_NestedLoop_IsReportedOnceByItsOwnScope()
    {
        const string sql = """
            WHILE DATALENGTH(@outer) > 0
            BEGIN
                WHILE DATALENGTH(@inner) > 0
                BEGIN
                    SET @inner = LEFT(@inner, LEN(@inner) - 1);
                END
                SET @outer = LEFT(@outer, DATALENGTH(@outer) - 1);
            END
            """;

        var diagnostics = _rule.Analyze(RuleTestContext.CreateContext(sql)).ToArray();

        Assert.Single(diagnostics);
    }
}
