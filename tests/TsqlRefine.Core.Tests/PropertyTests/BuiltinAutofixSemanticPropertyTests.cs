using FsCheck.Fluent;
using TsqlRefine.Core.Engine;
using TsqlRefine.PluginSdk;
using TsqlRefine.Rules;
using TsqlRefine.TestSupport;

namespace TsqlRefine.Core.Tests.PropertyTests;

public sealed class BuiltinAutofixSemanticPropertyTests
{
    private static readonly IRule[] FixableRules = new BuiltinRuleProvider().GetRules()
        .Where(rule => rule.Metadata.Fixable)
        .ToArray();

    [Fact]
    public void Fix_GrammarGeneratedSql_PreservesSyntaxAndResolvesOriginalLocation()
    {
        var engine = new TsqlRefineEngine(FixableRules);
        var samples = SqlGrammarGenerator.Scripts().Sample(200, 200);

        foreach (var sql in samples)
        {
            var input = new SqlInput("<generated>", sql);
            var before = Assert.Single(engine.Run("lint", [input], new EngineOptions()).Files);
            var after = Assert.Single(engine.Fix([input], new EngineOptions()).Files);

            Assert.DoesNotContain(after.Diagnostics, diagnostic =>
                diagnostic.Code is TsqlRefineEngine.ParseErrorCode or TsqlRefineEngine.ParserExceptionCode);

            foreach (var appliedFix in after.AppliedFixes)
            {
                var originalDiagnostics = before.Diagnostics
                    .Where(diagnostic => diagnostic.Data?.RuleId == appliedFix.RuleId)
                    .ToArray();
                Assert.NotEmpty(originalDiagnostics);
                Assert.DoesNotContain(after.Diagnostics, diagnostic =>
                    diagnostic.Data?.RuleId == appliedFix.RuleId &&
                    originalDiagnostics.Any(original => original.Range.Start == diagnostic.Range.Start));

                Assert.All(appliedFix.Edits, edit => Assert.Contains(
                    originalDiagnostics,
                    diagnostic => Contains(diagnostic.Range, edit.Range)));
            }
        }
    }

    private static bool Contains(TsqlRefine.PluginSdk.Range outer, TsqlRefine.PluginSdk.Range inner) =>
        Compare(outer.Start, inner.Start) <= 0 && Compare(inner.End, outer.End) <= 0;

    private static int Compare(Position left, Position right)
    {
        var lineComparison = left.Line.CompareTo(right.Line);
        return lineComparison != 0 ? lineComparison : left.Character.CompareTo(right.Character);
    }
}
