using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Metrics;
using TsqlRefine.Rules.Rules.Performance;
using TsqlRefine.Rules.Tests.Helpers;

namespace TsqlRefine.Rules.Tests.Performance;

public sealed class MetricThresholdRuleTests
{
    private const string ComplexProcedure = """
        CREATE PROCEDURE dbo.ComplexReport @first int, @second int
        AS
        BEGIN
            IF @first > 0
            BEGIN
                WHILE @second > 0
                BEGIN
                    SET @second -= 1;
                END;
            END;
            SELECT CASE WHEN @first = 1 THEN 1 WHEN @first = 2 THEN 2 ELSE 0 END
            FROM dbo.A AS a
            JOIN dbo.B AS b ON b.Id = a.Id
            JOIN dbo.C AS c ON c.Id = b.Id;
            SELECT 1;
        END;
        """;

    [Fact]
    public void Collect_ComplexProcedure_ReturnsExpectedMetrics()
    {
        var context = RuleTestContext.CreateContext(ComplexProcedure);

        var metrics = Assert.Single(SqlMetricsCollector.Collect(context.Ast.Fragment!));

        Assert.Equal("dbo.ComplexReport", metrics.Name);
        Assert.Equal("Procedure", metrics.Kind);
        Assert.Equal(5, metrics.CyclomaticComplexity);
        Assert.Equal(5, metrics.NestingDepth);
        Assert.True(metrics.StatementCount >= 6);
        Assert.Equal(2, metrics.MaxJoinsPerQuery);
        Assert.Equal(2, metrics.ParameterCount);
    }

    [Theory]
    [InlineData("max-cyclomatic-complexity")]
    [InlineData("max-nesting-depth")]
    [InlineData("max-statement-count")]
    [InlineData("max-joins-per-query")]
    [InlineData("max-parameter-count")]
    public void Analyze_ConfiguredMaximumExceeded_ReturnsDiagnostic(string ruleId)
    {
        var rule = CreateRule(ruleId);
        var context = RuleTestContext.CreateContext(ComplexProcedure) with
        {
            Settings = new RuleSettings(new MaximumOptions(1))
        };

        var diagnostic = Assert.Single(rule.Analyze(context));

        Assert.Equal(ruleId, diagnostic.Code);
        Assert.Contains("maximum of 1", diagnostic.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("max-cyclomatic-complexity")]
    [InlineData("max-nesting-depth")]
    [InlineData("max-statement-count")]
    [InlineData("max-joins-per-query")]
    [InlineData("max-parameter-count")]
    public void Analyze_SmallBatchWithDefaults_ReturnsEmpty(string ruleId)
    {
        var rule = CreateRule(ruleId);

        Assert.Empty(rule.Analyze(RuleTestContext.CreateContext("SELECT 1;")));
    }

    [Fact]
    public void OptionDescriptors_DefineBoundedMaximum()
    {
        var rule = new MaxCyclomaticComplexityRule();
        var provider = Assert.IsAssignableFrom<IRuleOptionsDescriptorProvider>(rule);

        var descriptor = Assert.Single(provider.OptionDescriptors);
        Assert.Equal("max", descriptor.Name);
        Assert.Equal(RuleOptionType.Number, descriptor.Type);
        Assert.Equal(1, descriptor.MinimumInt32);
        Assert.Equal(10000, descriptor.MaximumInt32);
    }

    private static IRule CreateRule(string ruleId) => ruleId switch
    {
        "max-cyclomatic-complexity" => new MaxCyclomaticComplexityRule(),
        "max-nesting-depth" => new MaxNestingDepthRule(),
        "max-statement-count" => new MaxStatementCountRule(),
        "max-joins-per-query" => new MaxJoinsPerQueryRule(),
        "max-parameter-count" => new MaxParameterCountRule(),
        _ => throw new ArgumentOutOfRangeException(nameof(ruleId))
    };

    private sealed class MaximumOptions(int maximum) : IRuleOptions
    {
        public bool TryGetBoolean(string name, out bool value)
        {
            value = default;
            return false;
        }

        public bool TryGetInt32(string name, out int value)
        {
            value = maximum;
            return string.Equals(name, "max", StringComparison.OrdinalIgnoreCase);
        }

        public bool TryGetString(string name, out string? value)
        {
            value = default;
            return false;
        }
    }
}

public sealed class MaxCyclomaticComplexityRuleTests
{
    [Fact]
    public void Metadata_UsesWarningSeverity() =>
        Assert.Equal(RuleSeverity.Warning, new MaxCyclomaticComplexityRule().Metadata.DefaultSeverity);
}

public sealed class MaxNestingDepthRuleTests
{
    [Fact]
    public void Metadata_UsesWarningSeverity() =>
        Assert.Equal(RuleSeverity.Warning, new MaxNestingDepthRule().Metadata.DefaultSeverity);
}

public sealed class MaxStatementCountRuleTests
{
    [Fact]
    public void Metadata_UsesInformationSeverity() =>
        Assert.Equal(RuleSeverity.Information, new MaxStatementCountRule().Metadata.DefaultSeverity);
}

public sealed class MaxJoinsPerQueryRuleTests
{
    [Fact]
    public void Metadata_UsesWarningSeverity() =>
        Assert.Equal(RuleSeverity.Warning, new MaxJoinsPerQueryRule().Metadata.DefaultSeverity);
}

public sealed class MaxParameterCountRuleTests
{
    [Fact]
    public void Metadata_UsesInformationSeverity() =>
        Assert.Equal(RuleSeverity.Information, new MaxParameterCountRule().Metadata.DefaultSeverity);
}
