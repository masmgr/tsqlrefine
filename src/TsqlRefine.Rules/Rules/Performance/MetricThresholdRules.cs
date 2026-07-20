using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Helpers.Metrics;

namespace TsqlRefine.Rules.Rules.Performance;

public abstract class MetricThresholdRuleBase(
    string ruleId,
    string description,
    RuleSeverity severity,
    int defaultMaximum) : IRule, IRuleOptionsDescriptorProvider
{
    public RuleMetadata Metadata { get; } = new(ruleId, description, "Performance", severity, false);

    public IReadOnlyList<RuleOptionDescriptor> OptionDescriptors { get; } =
    [
        new("max", RuleOptionType.Number, "Maximum allowed metric value.", 1, 10000)
    ];

    public IEnumerable<Diagnostic> Analyze(RuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.Ast.Fragment is null)
        {
            return [];
        }

        var maximum = context.Settings.Options?.TryGetInt32("max", out var configured) is true
            ? configured
            : defaultMaximum;
        return SqlMetricsCollector.Collect(context.Ast.Fragment)
            .Select(metric => (Metric: metric, Value: GetValue(metric)))
            .Where(item => item.Value > maximum)
            .Select(item => new Diagnostic(
                ScriptDomHelpers.GetRange(item.Metric.Location),
                $"{item.Metric.Kind} '{item.Metric.Name}' has {MetricDisplayName} {item.Value}, exceeding the configured maximum of {maximum}.",
                Code: Metadata.RuleId,
                Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, false)))
            .ToArray();
    }

    public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) =>
        RuleHelpers.NoFixes(context, diagnostic);

    protected abstract string MetricDisplayName { get; }
    protected abstract int GetValue(SqlObjectMetrics metrics);
}

public sealed class MaxCyclomaticComplexityRule()
    : MetricThresholdRuleBase(
        "max-cyclomatic-complexity",
        "Limits cyclomatic complexity per SQL object or batch.",
        RuleSeverity.Warning,
        20)
{
    protected override string MetricDisplayName => "cyclomatic complexity";
    protected override int GetValue(SqlObjectMetrics metrics) => metrics.CyclomaticComplexity;
}

public sealed class MaxNestingDepthRule()
    : MetricThresholdRuleBase(
        "max-nesting-depth",
        "Limits control-flow nesting depth per SQL object or batch.",
        RuleSeverity.Warning,
        5)
{
    protected override string MetricDisplayName => "nesting depth";
    protected override int GetValue(SqlObjectMetrics metrics) => metrics.NestingDepth;
}

public sealed class MaxStatementCountRule()
    : MetricThresholdRuleBase(
        "max-statement-count",
        "Limits executable statement count per SQL object or batch.",
        RuleSeverity.Information,
        200)
{
    protected override string MetricDisplayName => "statement count";
    protected override int GetValue(SqlObjectMetrics metrics) => metrics.StatementCount;
}

public sealed class MaxJoinsPerQueryRule()
    : MetricThresholdRuleBase(
        "max-joins-per-query",
        "Limits the number of joins in a single query.",
        RuleSeverity.Warning,
        8)
{
    protected override string MetricDisplayName => "maximum joins per query";
    protected override int GetValue(SqlObjectMetrics metrics) => metrics.MaxJoinsPerQuery;
}

public sealed class MaxParameterCountRule()
    : MetricThresholdRuleBase(
        "max-parameter-count",
        "Limits parameter count per procedure or function.",
        RuleSeverity.Information,
        15)
{
    protected override string MetricDisplayName => "parameter count";
    protected override int GetValue(SqlObjectMetrics metrics) => metrics.ParameterCount;
}
