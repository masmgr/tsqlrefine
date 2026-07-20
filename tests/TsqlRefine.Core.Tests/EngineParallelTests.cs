using System.Text.Json;
using TsqlRefine.Core.Engine;
using TsqlRefine.Rules.Rules.Style;

namespace TsqlRefine.Core.Tests;

public sealed class EngineParallelTests
{
    [Fact]
    public void Run_ManyInputs_PreservesOrderAndIsDeterministic()
    {
        var engine = new TsqlRefineEngine([new SemicolonTerminationRule()]);
        var inputs = CreateInputs();

        var first = engine.Run("lint", inputs, new EngineOptions());
        var second = engine.Run("lint", inputs, new EngineOptions());

        Assert.Equal(inputs.Select(input => input.FilePath), first.Files.Select(file => file.FilePath));
        Assert.Equal(JsonSerializer.Serialize(first.Files), JsonSerializer.Serialize(second.Files));
        Assert.Contains(first.Files, file => file.Diagnostics.Any(diagnostic => diagnostic.Code == "parse-error"));
        Assert.Contains(first.Files, file => file.Diagnostics.Any(diagnostic => diagnostic.Code == "semicolon-termination"));
    }

    [Fact]
    public void Fix_ManyInputs_PreservesOrderAndIsDeterministic()
    {
        var engine = new TsqlRefineEngine([new SemicolonTerminationRule()]);
        var inputs = CreateInputs();

        var first = engine.Fix(inputs, new EngineOptions());
        var second = engine.Fix(inputs, new EngineOptions());

        Assert.Equal(inputs.Select(input => input.FilePath), first.Files.Select(file => file.FilePath));
        Assert.Equal(JsonSerializer.Serialize(first.Files), JsonSerializer.Serialize(second.Files));
    }

    private static SqlInput[] CreateInputs() =>
        Enumerable.Range(0, 50)
            .Select(index => new SqlInput(
                $"input-{index:D2}.sql",
                (index % 3) switch
                {
                    0 => "SELECT 1",
                    1 => "SELECT 1;",
                    _ => "SELECT FROM;"
                }))
            .ToArray();
}
