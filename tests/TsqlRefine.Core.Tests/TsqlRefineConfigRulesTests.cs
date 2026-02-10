using System.Text.Json;
using TsqlRefine.Core.Config;

namespace TsqlRefine.Core.Tests;

public sealed class TsqlRefineConfigRulesTests
{
    [Fact]
    public void Deserialize_WithRulesMap_PopulatesDictionary()
    {
        var json = """
        {
            "compatLevel": 150,
            "rules": {
                "avoid-select-star": "none",
                "dml-without-where": "error",
                "avoid-nolock": "warning"
            }
        }
        """;

        var config = JsonSerializer.Deserialize<TsqlRefineConfig>(json, JsonDefaults.Options)!;

        Assert.NotNull(config.Rules);
        Assert.Equal(3, config.Rules.Count);
        Assert.Equal("none", config.Rules["avoid-select-star"]);
        Assert.Equal("error", config.Rules["dml-without-where"]);
        Assert.Equal("warning", config.Rules["avoid-nolock"]);
    }

    [Fact]
    public void Deserialize_WithoutRules_ReturnsNull()
    {
        var json = """{ "compatLevel": 150 }""";

        var config = JsonSerializer.Deserialize<TsqlRefineConfig>(json, JsonDefaults.Options)!;

        Assert.Null(config.Rules);
    }

    [Fact]
    public void Validate_WithValidRules_ReturnsNull()
    {
        var config = new TsqlRefineConfig(
            Rules: new Dictionary<string, string>
            {
                ["rule-a"] = "error",
                ["rule-b"] = "warning",
                ["rule-c"] = "info",
                ["rule-d"] = "inherit",
                ["rule-e"] = "none"
            });

        Assert.Null(config.Validate());
    }

    [Fact]
    public void Validate_WithInvalidSeverity_ReturnsError()
    {
        var config = new TsqlRefineConfig(
            Rules: new Dictionary<string, string>
            {
                ["rule-a"] = "critical"
            });

        var error = config.Validate();

        Assert.NotNull(error);
        Assert.Contains("rule-a", error);
        Assert.Contains("critical", error);
    }

    [Fact]
    public void Validate_WithNullRules_ReturnsNull()
    {
        var config = new TsqlRefineConfig(Rules: null);

        Assert.Null(config.Validate());
    }
}
