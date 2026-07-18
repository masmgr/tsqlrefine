using System.Text.Json;
using Json.Schema;
using TsqlRefine.Cli;
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
