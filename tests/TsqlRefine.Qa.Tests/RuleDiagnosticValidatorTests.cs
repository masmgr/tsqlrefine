using TsqlRefine.PluginSdk;

namespace TsqlRefine.Qa.Tests;

public sealed class RuleDiagnosticValidatorTests
{
    [Fact]
    public void ValidateFixes_TwoInsertionsAtSamePosition_ReportsOverlap()
    {
        var position = new Position(0, 0);
        var range = new TsqlRefine.PluginSdk.Range(position, position);
        var rule = new FixedRule(
            new Fix("Insert text", [new TextEdit(range, "a"), new TextEdit(range, "b")]));
        var diagnostic = new Diagnostic(range, "Test", Code: rule.Metadata.RuleId);
        var context = new RuleContext(
            "test.sql", 160, new ScriptDomAst(string.Empty), [], new RuleSettings());
        var failures = new List<string>();

        RuleDiagnosticValidator.ValidateFixes(
            rule, context, diagnostic, [string.Empty], "test.sql", failures);

        Assert.Contains(failures, failure => failure.Contains("overlapping edits", StringComparison.Ordinal));
    }

    private sealed class FixedRule(Fix fix) : IRule
    {
        public RuleMetadata Metadata { get; } = new(
            "fixed-rule", "Test rule", "Test", RuleSeverity.Warning, Fixable: true);

        public IEnumerable<Diagnostic> Analyze(RuleContext context) => [];

        public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic) => [fix];
    }
}
