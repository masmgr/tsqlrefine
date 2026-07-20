using System.Text.Json;
using TsqlRefine.Cli.Tests.Helpers;

namespace TsqlRefine.Cli.Tests;

[Collection("DirectoryChanging")]
public sealed class CliBaselineAndSarifTests
{
    [Fact]
    public async Task BaselineCreate_ThenLint_SuppressesExistingDiagnostics()
    {
        var context = await TestContext.CreateAsync("SELECT * FROM dbo.t;");
        try
        {
            var create = await RunAsync(
                ["baseline", "create", "--output", context.BaselinePath, context.SqlPath]);

            Assert.Equal(0, create.Code);
            Assert.True(File.Exists(context.BaselinePath));

            var lint = await RunAsync(
                ["lint", "--baseline", context.BaselinePath, "--output", "json", context.SqlPath]);

            Assert.Equal(0, lint.Code);
            using var output = JsonDocument.Parse(lint.Stdout);
            Assert.Empty(output.RootElement.GetProperty("files")[0].GetProperty("diagnostics").EnumerateArray());
            Assert.Contains("baseline-suppressed", lint.Stderr);
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Lint_ShowSuppressed_IncludesSuppressionMetadata()
    {
        var context = await TestContext.CreateAsync("SELECT * FROM dbo.t;");
        try
        {
            Assert.Equal(0, (await RunAsync(
                ["baseline", "create", "--output", context.BaselinePath, context.SqlPath])).Code);

            var lint = await RunAsync(
                ["lint", "--baseline", context.BaselinePath, "--show-suppressed", "--output", "json", context.SqlPath]);

            Assert.Equal(0, lint.Code);
            using var output = JsonDocument.Parse(lint.Stdout);
            var diagnostics = output.RootElement.GetProperty("files")[0].GetProperty("diagnostics");
            Assert.NotEmpty(diagnostics.EnumerateArray());
            Assert.All(diagnostics.EnumerateArray(), diagnostic =>
            {
                Assert.True(diagnostic.GetProperty("suppressed").GetBoolean());
                Assert.Equal(64, diagnostic.GetProperty("fingerprint").GetString()!.Length);
            });
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Lint_AfterNewViolationIsAdded_ReturnsViolation()
    {
        var context = await TestContext.CreateAsync("SELECT * FROM dbo.t;");
        try
        {
            Assert.Equal(0, (await RunAsync(
                ["baseline", "create", "--output", context.BaselinePath, context.SqlPath])).Code);
            await File.WriteAllTextAsync(
                context.SqlPath,
                "SELECT * FROM dbo.t;\nSELECT * FROM dbo.other_table;");

            var lint = await RunAsync(["lint", "--baseline", context.BaselinePath, context.SqlPath]);

            Assert.Equal(ExitCodes.Violations, lint.Code);
            Assert.Contains("avoid-select-star", lint.Stdout);
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task BaselineTrim_RemovesResolvedEntries()
    {
        var context = await TestContext.CreateAsync("SELECT * FROM dbo.t;");
        try
        {
            Assert.Equal(0, (await RunAsync(
                ["baseline", "create", "--output", context.BaselinePath, context.SqlPath])).Code);
            await File.WriteAllTextAsync(context.SqlPath, "SELECT id FROM dbo.t;");

            var trim = await RunAsync(
                ["baseline", "trim", "--baseline", context.BaselinePath, context.SqlPath]);

            Assert.Equal(0, trim.Code);
            using var baseline = JsonDocument.Parse(await File.ReadAllTextAsync(context.BaselinePath));
            Assert.Empty(baseline.RootElement.GetProperty("entries").EnumerateArray());
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task BaselineCreate_WithParseError_DoesNotWriteFile()
    {
        var context = await TestContext.CreateAsync("SELECT * FROM");
        try
        {
            var create = await RunAsync(
                ["baseline", "create", "--output", context.BaselinePath, context.SqlPath]);

            Assert.Equal(ExitCodes.AnalysisError, create.Code);
            Assert.False(File.Exists(context.BaselinePath));
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task Lint_Sarif_EmitsSarif21WithRuleAndFingerprint()
    {
        var result = await RunAsync(
            ["lint", "--stdin", "--output", "sarif"],
            "SELECT * FROM dbo.t;");

        Assert.Equal(ExitCodes.Violations, result.Code);
        using var sarif = JsonDocument.Parse(result.Stdout);
        Assert.Equal("2.1.0", sarif.RootElement.GetProperty("version").GetString());
        var run = sarif.RootElement.GetProperty("runs")[0];
        Assert.Equal("tsqlrefine", run.GetProperty("tool").GetProperty("driver").GetProperty("name").GetString());
        var sarifResult = run.GetProperty("results").EnumerateArray()
            .First(item => item.GetProperty("ruleId").GetString() == "avoid-select-star");
        Assert.Equal("warning", sarifResult.GetProperty("level").GetString());
        Assert.Equal(
            64,
            sarifResult.GetProperty("partialFingerprints").GetProperty("tsqlrefine/v1").GetString()!.Length);
    }

    private static async Task<CliResult> RunAsync(string[] args, string? stdin = null)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await CliApp.RunAsync(args, new StringReader(stdin ?? string.Empty), stdout, stderr);
        return new CliResult(code, stdout.ToString(), stderr.ToString());
    }

    private sealed record CliResult(int Code, string Stdout, string Stderr);

    private sealed class TestContext : IAsyncDisposable
    {
        private readonly string _originalDirectory;

        private TestContext(string directory, string originalDirectory, string sqlPath, string baselinePath)
        {
            Directory = directory;
            _originalDirectory = originalDirectory;
            SqlPath = sqlPath;
            BaselinePath = baselinePath;
        }

        public string Directory { get; }
        public string SqlPath { get; }
        public string BaselinePath { get; }

        public static async Task<TestContext> CreateAsync(string sql)
        {
            var originalDirectory = Environment.CurrentDirectory;
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            Environment.CurrentDirectory = directory;
            var sqlPath = Path.Combine(directory, "input.sql");
            var baselinePath = Path.Combine(directory, ".tsqlrefine", "baseline.json");
            await File.WriteAllTextAsync(sqlPath, sql);
            return new TestContext(directory, originalDirectory, sqlPath, baselinePath);
        }

        public async ValueTask DisposeAsync()
        {
            await TempDirectoryCleanup.CleanupAsync(_originalDirectory, Directory);
        }
    }
}
