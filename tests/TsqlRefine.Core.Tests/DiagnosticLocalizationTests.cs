using System.Globalization;
using TsqlRefine.Core.Localization;
using TsqlRefine.PluginSdk;
using SdkRange = TsqlRefine.PluginSdk.Range;

namespace TsqlRefine.Core.Tests;

public sealed class DiagnosticLocalizationTests
{
    [Fact]
    public void Localize_BuiltInResource_ReturnsResourceText()
    {
        var diagnostic = new Diagnostic(
            new SdkRange(new Position(0, 0), new Position(0, 1)),
            "fallback")
        {
            Localization = new DiagnosticMessage("tsqlrefine.rule.avoid-select-star")
        };

        var result = new DiagnosticLocalizer().Localize(diagnostic, CultureInfo.InvariantCulture);

        Assert.Equal("Avoid SELECT *; explicitly list required columns.", result.Message);
    }

    [Fact]
    public void Localize_PluginResource_UsesNamedArguments()
    {
        var provider = new TestLocalizationProvider(
            new Dictionary<string, string>
            {
                ["custom.rule"] = "テーブル {tableName} に問題があります。"
            });
        var diagnostic = new Diagnostic(
            new SdkRange(new Position(0, 0), new Position(0, 1)),
            "fallback")
        {
            Localization = new DiagnosticMessage(
                "custom.rule",
                new Dictionary<string, object?> { ["tableName"] = "Customer" })
        };

        var result = new DiagnosticLocalizer([provider]).Localize(
            diagnostic,
            CultureInfo.GetCultureInfo("ja-JP"));

        Assert.Equal("テーブル Customer に問題があります。", result.Message);
    }

    [Fact]
    public void Localize_MissingKey_PreservesOriginalMessage()
    {
        var diagnostic = new Diagnostic(
            new SdkRange(new Position(0, 0), new Position(0, 1)),
            "fallback");

        var result = new DiagnosticLocalizer().Localize(
            diagnostic,
            CultureInfo.GetCultureInfo("ja-JP"));

        Assert.Equal("fallback", result.Message);
    }

    private sealed class TestLocalizationProvider(IReadOnlyDictionary<string, string> strings)
        : IDiagnosticLocalizationProvider
    {
        public string Name => "test";

        public string? GetString(string key, CultureInfo culture) =>
            strings.TryGetValue(key, out var value) ? value : null;
    }
}
