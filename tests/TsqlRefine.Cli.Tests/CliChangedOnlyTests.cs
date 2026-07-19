using System.Diagnostics;
using System.Text.Json;
using TsqlRefine.Cli.Services;
using TsqlRefine.Cli.Tests.Helpers;
using TsqlRefine.Core;

namespace TsqlRefine.Cli.Tests;

[Collection("DirectoryChanging")]
public sealed class CliChangedOnlyTests
{
    [Fact]
    public async Task ChangedLinesFile_FiltersDiagnosticsToIntersectingLines()
    {
        var context = await TestContext.CreateAsync(
            "SELECT * FROM dbo.FirstTable;\nSELECT * FROM dbo.SecondTable;");
        try
        {
            await WriteChangedLinesAsync(context, 1, new ChangedLineRange(2, 2));

            var result = await RunAsync([
                "lint", "--changed-lines-from", context.ChangedLinesPath,
                "--output", "json", "--quiet", context.SqlPath
            ]);

            Assert.Equal(ExitCodes.Violations, result.Code);
            using var document = JsonDocument.Parse(result.Stdout);
            var selectStar = document.RootElement.GetProperty("files")[0].GetProperty("diagnostics")
                .EnumerateArray()
                .Where(diagnostic => diagnostic.GetProperty("code").GetString() == "avoid-select-star")
                .ToArray();
            var diagnostic = Assert.Single(selectStar);
            Assert.Equal(1, diagnostic.GetProperty("range").GetProperty("start").GetProperty("line").GetInt32());
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task ChangedLinesFile_ParseErrorOutsideRange_RemainsVisible()
    {
        var context = await TestContext.CreateAsync("SELECT (\nSELECT 1;");
        try
        {
            await WriteChangedLinesAsync(context, 1, new ChangedLineRange(2, 2));

            var result = await RunAsync([
                "lint", "--changed-lines-from", context.ChangedLinesPath,
                "--output", "json", "--quiet", context.SqlPath
            ]);

            Assert.Equal(ExitCodes.AnalysisError, result.Code);
            using var document = JsonDocument.Parse(result.Stdout);
            Assert.Contains(
                document.RootElement.GetProperty("files")[0].GetProperty("diagnostics").EnumerateArray(),
                diagnostic => diagnostic.GetProperty("code").GetString() == "parse-error");
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task ChangedOnly_GitWorkingTree_UsesModifiedLines()
    {
        var context = await TestContext.CreateAsync(
            "SELECT * FROM dbo.FirstTable;\nSELECT Id FROM dbo.SecondTable;");
        try
        {
            await RunGitAsync(context.Directory, "init");
            await RunGitAsync(context.Directory, "config", "user.email", "test@example.com");
            await RunGitAsync(context.Directory, "config", "user.name", "TsqlRefine Test");
            await RunGitAsync(context.Directory, "add", "input.sql");
            await RunGitAsync(context.Directory, "commit", "-m", "initial");
            await File.WriteAllTextAsync(
                context.SqlPath,
                "SELECT * FROM dbo.FirstTable;\nSELECT * FROM dbo.SecondTable;");
            Environment.CurrentDirectory = context.Directory;

            var result = await RunAsync([
                "lint", "--changed-only", "--base-ref", "HEAD",
                "--output", "json", "--quiet", context.SqlPath
            ]);

            Assert.Equal(ExitCodes.Violations, result.Code);
            using var document = JsonDocument.Parse(result.Stdout);
            var selectStar = document.RootElement.GetProperty("files")[0].GetProperty("diagnostics")
                .EnumerateArray()
                .Where(diagnostic => diagnostic.GetProperty("code").GetString() == "avoid-select-star")
                .ToArray();
            Assert.Single(selectStar);
            Assert.Equal(1, selectStar[0].GetProperty("range").GetProperty("start").GetProperty("line").GetInt32());
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    [Fact]
    public async Task ChangedLinesFile_UnsupportedVersion_ReturnsConfigError()
    {
        var context = await TestContext.CreateAsync("SELECT 1;");
        try
        {
            await WriteChangedLinesAsync(context, 2, new ChangedLineRange(1, 1));

            var result = await RunAsync([
                "lint", "--changed-lines-from", context.ChangedLinesPath, context.SqlPath
            ]);

            Assert.Equal(ExitCodes.ConfigError, result.Code);
            Assert.Contains("Unsupported changed-lines version", result.Stderr);
        }
        finally
        {
            await context.DisposeAsync();
        }
    }

    private static async Task WriteChangedLinesAsync(
        TestContext context,
        int version,
        ChangedLineRange range)
    {
        var document = new ChangedLinesDocument(
            version,
            [new ChangedFileLines("input.sql", [range])]);
        await File.WriteAllTextAsync(
            context.ChangedLinesPath,
            JsonSerializer.Serialize(document, JsonDefaults.Options));
    }

    private static async Task RunGitAsync(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        using var process = Process.Start(startInfo)!;
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, await process.StandardError.ReadToEndAsync());
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
        private readonly string _originalDirectory;

        private TestContext(string originalDirectory, string directory)
        {
            _originalDirectory = originalDirectory;
            Directory = directory;
            SqlPath = Path.Combine(directory, "input.sql");
            ChangedLinesPath = Path.Combine(directory, "changed-lines.json");
        }

        internal string Directory { get; }
        internal string SqlPath { get; }
        internal string ChangedLinesPath { get; }

        internal static async Task<TestContext> CreateAsync(string sql)
        {
            var originalDirectory = Environment.CurrentDirectory;
            var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            var context = new TestContext(originalDirectory, directory);
            await File.WriteAllTextAsync(context.SqlPath, sql);
            return context;
        }

        public async ValueTask DisposeAsync()
        {
            var gitDirectory = Path.Combine(Directory, ".git");
            if (System.IO.Directory.Exists(gitDirectory))
            {
                foreach (var path in System.IO.Directory.EnumerateFiles(gitDirectory, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                }
            }
            await TempDirectoryCleanup.CleanupAsync(_originalDirectory, Directory);
        }
    }
}
