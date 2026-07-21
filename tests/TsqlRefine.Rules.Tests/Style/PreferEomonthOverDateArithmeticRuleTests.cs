using TsqlRefine.Rules.Rules.Style;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Style;

public sealed class PreferEomonthOverDateArithmeticRuleTests
{
    private readonly PreferEomonthOverDateArithmeticRule _rule = new();

    [Theory]
    [InlineData("SELECT DATEADD(day, -1, DATEADD(month, 1, DATEADD(month, DATEDIFF(month, 0, @date), 0)));")]
    [InlineData("SELECT DATEADD(dd, -1, DATEADD(mm, 1, DATEADD(mm, DATEDIFF(mm, 0, MAX(DT)), 0))) FROM T;")]
    public void Analyze_LegacyMonthEnd_ReturnsDiagnostic(string sql) =>
        Assert.Single(_rule.Analyze(RuleTestContext.CreateContext(sql)));

    [Theory]
    [InlineData("SELECT EOMONTH(@date);")]
    [InlineData("SELECT DATEADD(day, 1, DATEADD(month, 1, @date));")]
    [InlineData("SELECT DATEADD(day, -1, DATEADD(month, 1, @date));")]
    public void Analyze_OtherExpression_ReturnsEmpty(string sql) =>
        Assert.Empty(_rule.Analyze(RuleTestContext.CreateContext(sql)));
}
