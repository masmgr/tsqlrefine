using TsqlRefine.Core.Engine;
using TsqlRefine.Rules;

namespace TsqlRefine.Qa.Tests;

public sealed class CompatibilityMatrixTests
{
    private static readonly int[] CompatibilityLevels = [110, 120, 130, 140, 150, 160];

    [Fact]
    public void Lint_CorpusAcrossCompatibilityLevels_DoesNotCrashAndParsesAtMinimumLevel()
    {
        var engine = new TsqlRefineEngine(new BuiltinRuleProvider().GetRules());
        foreach (var file in CorpusSupport.LoadFiles())
        {
            var sql = CorpusSupport.Read(file);
            foreach (var level in CompatibilityLevels)
            {
                var result = engine.Run("lint", [new SqlInput(file.Path, sql)], new EngineOptions(level));
                Assert.DoesNotContain(result.Files[0].Diagnostics, diagnostic =>
                    diagnostic.Code == TsqlRefineEngine.ParserExceptionCode ||
                    diagnostic.Message.Contains(" crashed:", StringComparison.Ordinal));

                if (level >= file.MinCompatLevel)
                {
                    Assert.Empty(CorpusSupport.Parse(sql, level));
                }
            }
        }
    }
}
