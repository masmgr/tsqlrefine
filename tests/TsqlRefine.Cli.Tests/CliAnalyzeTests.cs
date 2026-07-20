using System.Text.Json;
using TsqlRefine.Cli.Tests.Helpers;
using TsqlRefine.Schema.Catalog;

namespace TsqlRefine.Cli.Tests;

public sealed class CliAnalyzeTests
{
    [Fact]
    public async Task AnalyzeImpact_ReturnsDirectAndTransitiveDependents()
    {
        var context = await TestContext.CreateAsync();
        try
        {
            var result = await RunAsync([
                "analyze", "impact",
                "--catalog", context.CatalogPath,
                "--table", "dbo.Users",
                "--column", "Email"
            ]);

            Assert.Equal(0, result.Code);
            using var document = JsonDocument.Parse(result.Stdout);
            var impacted = document.RootElement.GetProperty("impactedObjects").EnumerateArray().ToArray();
            Assert.Equal(3, impacted.Length);
            Assert.Equal("dbo.DirectImpact", impacted[0].GetProperty("id").GetString());
            Assert.Equal(1, impacted[0].GetProperty("depth").GetInt32());
            Assert.Equal("dbo.ProcedureImpact", impacted[2].GetProperty("id").GetString());
            Assert.Equal(3, impacted[2].GetProperty("depth").GetInt32());
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task AnalyzeGraph_Json_WritesDependencyGraph()
    {
        var context = await TestContext.CreateAsync();
        try
        {
            var outputPath = Path.Combine(context.Directory, "graph.json");
            var result = await RunAsync([
                "analyze", "graph",
                "--catalog", context.CatalogPath,
                "--output", outputPath
            ]);

            Assert.Equal(0, result.Code);
            Assert.Empty(result.Stdout);
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            Assert.Equal(3, document.RootElement.GetProperty("nodes").GetArrayLength());
            Assert.Contains(document.RootElement.GetProperty("edges").EnumerateArray(), edge =>
                edge.GetProperty("from").GetString() == "dbo.IndirectImpact" &&
                edge.GetProperty("to").GetString() == "dbo.DirectImpact");
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task AnalyzeGraph_Dot_WritesSelfContainedDotFile()
    {
        var context = await TestContext.CreateAsync();
        try
        {
            var outputPath = Path.Combine(context.Directory, "graph.dot");
            var result = await RunAsync([
                "analyze", "graph",
                "--catalog", context.CatalogPath,
                "--format", "dot",
                "--output", outputPath
            ]);

            Assert.Equal(0, result.Code);
            var dot = await File.ReadAllTextAsync(outputPath);
            Assert.StartsWith("digraph tsqlrefine", dot);
            Assert.Contains("\"dbo.IndirectImpact\" -> \"dbo.DirectImpact\"", dot);
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task AnalyzeImpact_WithoutCatalog_ReturnsConfigError()
    {
        var result = await RunAsync(["analyze", "impact", "--table", "dbo.Users"]);

        Assert.Equal(ExitCodes.ConfigError, result.Code);
        Assert.Contains("--catalog", result.Stderr);
    }

    private static async Task<CliResult> RunAsync(string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await CliApp.RunAsync(args, TextReader.Null, stdout, stderr);
        return new CliResult(code, stdout.ToString(), stderr.ToString());
    }

    private sealed record CliResult(int Code, string Stdout, string Stderr);

    private sealed class TestContext : IAsyncDisposable
    {
        private TestContext(string directory, string catalogPath)
        {
            Directory = directory;
            CatalogPath = catalogPath;
        }

        internal string Directory { get; }
        internal string CatalogPath { get; }

        internal static async Task<TestContext> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var catalog = ObjectCatalogCollector.Collect(
            [
                ("CREATE VIEW dbo.DirectImpact AS SELECT u.Email FROM dbo.Users AS u;", "direct.sql"),
                ("CREATE VIEW dbo.IndirectImpact AS SELECT Email FROM dbo.DirectImpact;", "indirect.sql"),
                ("CREATE PROCEDURE dbo.ProcedureImpact AS SELECT Email FROM dbo.IndirectImpact;", "procedure.sql")
            ], 150);
            var catalogPath = Path.Combine(directory, "objects.json");
            await File.WriteAllTextAsync(catalogPath, ObjectCatalogSerializer.Serialize(catalog));
            return new TestContext(directory, catalogPath);
        }

        public async ValueTask DisposeAsync() =>
            await TempDirectoryCleanup.CleanupAsync(Environment.CurrentDirectory, Directory);
    }
}
