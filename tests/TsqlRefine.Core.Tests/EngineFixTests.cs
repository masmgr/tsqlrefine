using TsqlRefine.Core.Engine;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules.Rules;

namespace TsqlRefine.Core.Tests;

public sealed class EngineFixTests
{
    [Fact]
    public void Fix_WhenRequireAsForColumnAliasRule_InsertsAsKeyword()
    {
        var rules = new IRule[] { new RequireAsForColumnAliasRule() };
        var engine = new TsqlRefineEngine(rules);

        var result = engine.Fix(
            inputs: new[] { new SqlInput("a.sql", "SELECT id userId FROM users;") },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        Assert.Equal("SELECT id AS userId FROM users;", result.Files[0].FixedText);
        Assert.Empty(result.Files[0].Diagnostics);
        Assert.Single(result.Files[0].AppliedFixes);
    }

    [Fact]
    public void Fix_WhenRequireAsForTableAliasRule_InsertsAsKeyword()
    {
        var rules = new IRule[] { new RequireAsForTableAliasRule() };
        var engine = new TsqlRefineEngine(rules);

        var result = engine.Fix(
            inputs: new[] { new SqlInput("a.sql", "SELECT * FROM users u;") },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        Assert.Equal("SELECT * FROM users AS u;", result.Files[0].FixedText);
        Assert.Empty(result.Files[0].Diagnostics);
        Assert.Single(result.Files[0].AppliedFixes);
    }

    [Fact]
    public void Fix_WhenFixableRule_AppliesFixAndClearsDiagnostics()
    {
        var rules = new IRule[] { new StarFixRule("rule-a", "id") };
        var engine = new TsqlRefineEngine(rules);

        var result = engine.Fix(
            inputs: new[] { new SqlInput("a.sql", "select * from t;") },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        Assert.Equal("select id from t;", result.Files[0].FixedText);
        Assert.Empty(result.Files[0].Diagnostics);
        Assert.Single(result.Files[0].AppliedFixes);
    }

    [Fact]
    public void Fix_WhenFixesConflict_SkipsLaterFix()
    {
        var rules = new IRule[]
        {
            new StarFixRule("rule-a", "id"),
            new StarFixRule("rule-b", "name")
        };
        var engine = new TsqlRefineEngine(rules);

        var result = engine.Fix(
            inputs: new[] { new SqlInput("a.sql", "select * from t;") },
            options: new EngineOptions()
        );

        Assert.Single(result.Files);
        Assert.Equal("select id from t;", result.Files[0].FixedText);
        Assert.Contains(result.Files[0].SkippedFixes, f => f.RuleId == "rule-b");
    }

    private sealed class StarFixRule : IRule
    {
        private readonly string _replacement;

        public StarFixRule(string ruleId, string replacement)
        {
            _replacement = replacement;
            Metadata = new RuleMetadata(
                RuleId: ruleId,
                Description: "Replace select star.",
                Category: "Style",
                DefaultSeverity: RuleSeverity.Warning,
                Fixable: true
            );
        }

        public RuleMetadata Metadata { get; }

        public IEnumerable<Diagnostic> Analyze(RuleContext context)
        {
            var index = context.Ast.RawSql.IndexOf("*", StringComparison.Ordinal);
            if (index < 0)
            {
                yield break;
            }

            var start = ToPosition(context.Ast.RawSql, index);
            var end = new Position(start.Line, start.Character + 1);

            yield return new Diagnostic(
                Range: new TsqlRefine.PluginSdk.Range(start, end),
                Message: "Avoid select star.",
                Severity: null,
                Code: Metadata.RuleId,
                Data: new DiagnosticData(Metadata.RuleId, Metadata.Category, Metadata.Fixable)
            );
        }

        public IEnumerable<Fix> GetFixes(RuleContext context, Diagnostic diagnostic)
        {
            yield return new Fix(
                Title: "Replace star",
                Edits: new[] { new TextEdit(diagnostic.Range, _replacement) }
            );
        }

        private static Position ToPosition(string text, int index)
        {
            var line = 0;
            var character = 0;

            for (var i = 0; i < index && i < text.Length; i++)
            {
                var ch = text[i];
                if (ch == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }

                    line++;
                    character = 0;
                    continue;
                }

                if (ch == '\n')
                {
                    line++;
                    character = 0;
                    continue;
                }

                character++;
            }

            return new Position(line, character);
        }
    }
}
