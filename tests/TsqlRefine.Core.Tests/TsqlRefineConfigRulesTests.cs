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
        Assert.Equal("none", config.Rules["avoid-select-star"].Severity);
        Assert.Equal("error", config.Rules["dml-without-where"].Severity);
        Assert.Equal("warning", config.Rules["avoid-nolock"].Severity);
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
            Rules: new Dictionary<string, RuleConfig>
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
            Rules: new Dictionary<string, RuleConfig>
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

    [Fact]
    public void Validate_WithInvalidLocale_ReturnsError()
    {
        var error = new TsqlRefineConfig(Locale: "not_locale").Validate();

        Assert.Contains("Invalid locale", error);
    }

    [Fact]
    public void Deserialize_WithLocale_PopulatesLocale()
    {
        var config = JsonSerializer.Deserialize<TsqlRefineConfig>("{ \"locale\": \"ja-JP\" }", JsonDefaults.Options)!;

        Assert.Equal("ja-JP", config.Locale);
        Assert.Null(config.Validate());
    }

    [Fact]
    public void Deserialize_WithRuleOptions_PopulatesTypedValues()
    {
        var json = """
        {
          "rules": {
            "metric-rule": {
              "severity": "warning",
              "options": { "max": 12, "enabled": true, "label": "team" }
            }
          }
        }
        """;

        var config = JsonSerializer.Deserialize<TsqlRefineConfig>(json, JsonDefaults.Options)!;
        var rule = config.Rules!["metric-rule"];

        Assert.Equal("warning", rule.Severity);
        Assert.Equal(12, rule.Options!["max"].Int32Value);
        Assert.True(rule.Options["enabled"].BooleanValue);
        Assert.Equal("team", rule.Options["label"].StringValue);
    }

    [Fact]
    public void Serialize_WithRuleOptions_WritesObjectForm()
    {
        var config = new TsqlRefineConfig(Rules: new Dictionary<string, RuleConfig>
        {
            ["metric-rule"] = new("warning", new Dictionary<string, RuleOptionValue>
            {
                ["max"] = RuleOptionValue.FromInt32(12)
            })
        });

        var json = JsonSerializer.Serialize(config, JsonDefaults.Options);

        Assert.Contains("\"options\"", json, StringComparison.Ordinal);
        Assert.Contains("\"max\": 12", json, StringComparison.Ordinal);
    }
}
