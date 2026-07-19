using System.Text.Json;
using Json.Schema;
using TsqlRefine.Cli;
using TsqlRefine.Cli.Services;
using TsqlRefine.Core;
using TsqlRefine.Core.Engine;
using TsqlRefine.Rules;

namespace TsqlRefine.Qa.Tests;

public sealed class OutputContractTests
{
    [Fact]
    public void JsonOutput_ContainsFieldsRequiredByCommittedSchemas()
    {
        var engine = new TsqlRefineEngine(new BuiltinRuleProvider().GetRules());
        var input = new SqlInput("contract.sql", "SELECT * FROM dbo.Users;");

        AssertMatchesSchema(
            engine.Run("lint", [input], new EngineOptions()),
            Path.Combine(CorpusSupport.RepositoryRoot, "schemas", "lint-result.schema.json"));
        AssertMatchesSchema(
            engine.Fix([input], new EngineOptions()),
            Path.Combine(CorpusSupport.RepositoryRoot, "schemas", "fix-result.schema.json"));
        AssertMatchesSchema(
            new BaselineDocument(
                Version: 1,
                FingerprintVersion: 1,
                GeneratedAt: DateTimeOffset.UtcNow,
                ToolVersion: "1.0.0",
                Root: "..",
                Entries:
                [
                    new BaselineEntry(
                        new string('a', 64),
                        "avoid-select-star",
                        "contract.sql")
                ]),
            Path.Combine(CorpusSupport.RepositoryRoot, "schemas", "baseline.schema.json"));
        AssertMatchesSchema(
            new ReportDocument(
                SchemaVersion: 1,
                Tool: "tsqlrefine",
                Version: "1.0.0",
                GeneratedAt: DateTimeOffset.UtcNow,
                Summary: new ReportSummary(1, 1, 0, 1, 0, 0),
                DiagnosticsByCategory: [new ReportCount("Performance", 1)],
                DiagnosticsByRule: [new ReportCount("avoid-select-star", 1)],
                DiagnosticsByFile: [new ReportCount("contract.sql", 1)],
                TopComplexObjects:
                [
                    new ReportMetric("contract.sql", "batch-1", "Batch", 1, 1, 0, 1, 0, 0)
                ],
                Baseline: new ReportBaselineSummary(1, 0, 0)),
            Path.Combine(CorpusSupport.RepositoryRoot, "schemas", "report-result.schema.json"));
    }

    [Fact]
    public async Task ExitCodes_RemainStableForAutomation()
    {
        Assert.Equal(0, await RunAsync(["lint", "--stdin"], "SELECT 1;"));
        Assert.Equal(ExitCodes.Violations, await RunAsync(["lint", "--stdin"], "SELECT * FROM dbo.Users;"));
        Assert.Equal(ExitCodes.AnalysisError, await RunAsync(["lint", "--stdin"], "SELECT ("));
        Assert.Equal(ExitCodes.ConfigError, await RunAsync(["lint", "--stdin", "--preset", "does-not-exist"], "SELECT 1;"));
        Assert.Equal(ExitCodes.Fatal, await RunAsync(["lint", "definitely-does-not-exist.sql"], string.Empty));
    }

    private static async Task<int> RunAsync(string[] args, string stdin) =>
        await CliApp.RunAsync(args, new StringReader(stdin), TextWriter.Null, TextWriter.Null);

    private static void AssertMatchesSchema<T>(T value, string schemaPath)
    {
        var schema = JsonSchema.FromText(File.ReadAllText(schemaPath));
        using var output = JsonDocument.Parse(JsonSerializer.Serialize(value, JsonDefaults.Options));
        var result = schema.Evaluate(output.RootElement, new EvaluationOptions { OutputFormat = OutputFormat.List });
        Assert.True(result.IsValid, result.ToString());
    }
}
