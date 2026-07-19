using System.Text.Json;
using TsqlRefine.Cli.Tests.Helpers;

namespace TsqlRefine.Cli.Tests;

[Collection("DirectoryChanging")]
public sealed class CliReportTests
{
    [Fact]
    public async Task Report_Json_IncludesAggregationsAndMetrics()
    {
        const string sql = "CREATE PROCEDURE dbo.p @value int AS BEGIN IF @value > 0 SELECT * FROM dbo.t; END;";

        var result = await RunAsync(["report", "--stdin", "--output-format", "json", "--quiet"], sql);

        Assert.Equal(0, result.Code);
        using var document = JsonDocument.Parse(result.Stdout);
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(1, root.GetProperty("summary").GetProperty("fileCount").GetInt32());
        Assert.NotEmpty(root.GetProperty("diagnosticsByRule").EnumerateArray());
        var metric = Assert.Single(root.GetProperty("topComplexObjects").EnumerateArray());
        Assert.Equal("dbo.p", metric.GetProperty("name").GetString());
        Assert.Equal(2, metric.GetProperty("cyclomaticComplexity").GetInt32());
        Assert.False(root.TryGetProperty("baseline", out _));
    }

    [Fact]
    public async Task Report_HtmlOutputFile_IsSelfContained()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var outputPath = Path.Combine(directory, "report.html");
        try
        {
            var result = await RunAsync(
                ["report", "--stdin", "--output-format", "html", "--output", outputPath],
                "SELECT * FROM dbo.t;");

            Assert.Equal(0, result.Code);
            Assert.Empty(result.Stdout);
            var html = await File.ReadAllTextAsync(outputPath);
            Assert.Contains("<!doctype html>", html);
            Assert.Contains("<style>", html);
            Assert.Contains("<script>", html);
            Assert.DoesNotContain("<link", html, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("avoid-select-star", html);
        }
        finally
        {
            await TempDirectoryCleanup.CleanupAsync(Environment.CurrentDirectory, directory);
        }
    }

    [Fact]
    public async Task Report_WithBaseline_ReportsFrozenAndResolvedCounts()
    {
        var originalDirectory = Environment.CurrentDirectory;
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        Environment.CurrentDirectory = directory;
        var sqlPath = Path.Combine(directory, "input.sql");
        var baselinePath = Path.Combine(directory, "baseline.json");
        try
        {
            await File.WriteAllTextAsync(sqlPath, "SELECT * FROM dbo.t;");
            Assert.Equal(0, (await RunAsync(
                ["baseline", "create", "--output", baselinePath, sqlPath])).Code);

            var frozen = await RunAsync(
                ["report", "--baseline", baselinePath, "--quiet", sqlPath]);
            using var frozenDocument = JsonDocument.Parse(frozen.Stdout);
            Assert.True(frozenDocument.RootElement.GetProperty("baseline").GetProperty("frozenCount").GetInt32() > 0);
            Assert.Equal(0, frozenDocument.RootElement.GetProperty("baseline").GetProperty("resolvedCount").GetInt32());

            await File.WriteAllTextAsync(sqlPath, "SELECT id FROM dbo.t;");
            var resolved = await RunAsync(
                ["report", "--baseline", baselinePath, "--quiet", sqlPath]);
            using var resolvedDocument = JsonDocument.Parse(resolved.Stdout);
            Assert.Equal(0, resolvedDocument.RootElement.GetProperty("baseline").GetProperty("frozenCount").GetInt32());
            Assert.True(resolvedDocument.RootElement.GetProperty("baseline").GetProperty("resolvedCount").GetInt32() > 0);
        }
        finally
        {
            await TempDirectoryCleanup.CleanupAsync(originalDirectory, directory);
        }
    }

    [Fact]
    public async Task Report_InvalidOutputFormat_ReturnsConfigError()
    {
        var result = await RunAsync(["report", "--stdin", "--output-format", "xml"], "SELECT 1;");

        Assert.Equal(ExitCodes.ConfigError, result.Code);
        Assert.Contains("Invalid --output-format", result.Stderr);
    }

    private static async Task<CliResult> RunAsync(string[] args, string? stdin = null)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await CliApp.RunAsync(args, new StringReader(stdin ?? string.Empty), stdout, stderr);
        return new CliResult(code, stdout.ToString(), stderr.ToString());
    }

    private sealed record CliResult(int Code, string Stdout, string Stderr);
}
