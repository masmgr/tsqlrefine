using TsqlRefine.Core.Engine;
using TsqlRefine.Rules;

namespace TsqlRefine.Qa.Tests;

public sealed class CorpusFixTests
{
    private const int MaximumPasses = 5;

    [Fact]
    public void Fix_AllCorpusFiles_RemainsParseableAndConverges()
    {
        var engine = new TsqlRefineEngine(new BuiltinRuleProvider().GetRules());
        foreach (var file in CorpusSupport.LoadFiles())
        {
            var options = new EngineOptions(CompatLevel: file.MinCompatLevel);
            var text = CorpusSupport.Read(file);
            var converged = false;

            for (var pass = 0; pass < MaximumPasses; pass++)
            {
                var before = engine.Run("lint", [new SqlInput(file.Path, text)], options).Files[0].Diagnostics;
                var fixedFile = engine.Fix([new SqlInput(file.Path, text)], options).Files[0];
                Assert.Empty(CorpusSupport.Parse(fixedFile.FixedText, file.MinCompatLevel));

                foreach (var applied in fixedFile.AppliedFixes)
                {
                    var originalDiagnostics = before.Where(diagnostic =>
                        string.Equals(diagnostic.Data?.RuleId ?? diagnostic.Code, applied.RuleId, StringComparison.Ordinal));
                    foreach (var original in originalDiagnostics)
                    {
                        Assert.DoesNotContain(fixedFile.Diagnostics, remaining =>
                            remaining.Message == original.Message && remaining.Range == original.Range);
                    }
                }

                if (fixedFile.FixedText == text)
                {
                    converged = true;
                    break;
                }

                text = fixedFile.FixedText;
            }

            Assert.True(converged, $"Fixes for '{file.Path}' did not converge within {MaximumPasses} passes.");
        }
    }
}
