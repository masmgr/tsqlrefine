using System.Text.Json;
using TsqlRefine.Cli.Tests.Helpers;
using TsqlRefine.Schema.Catalog;
using TsqlRefine.Schema.Model;
using TsqlRefine.Schema.Snapshot;
using TsqlRefine.Schema.TypeSystem;

namespace TsqlRefine.Cli.Tests;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506", Justification = "CLI integration test intentionally covers schema, catalog, and output boundaries.")]
public sealed class SchemaDiffCommandTests
{
    [Fact]
    public async Task SchemaDiff_BreakingColumnRemoval_ReturnsViolationAndImpact()
    {
        var directory = CreateDirectory();
        try
        {
            var beforePath = await WriteSnapshotAsync(directory, "before.json", includeEmail: true);
            var afterPath = await WriteSnapshotAsync(directory, "after.json", includeEmail: false);
            var catalog = ObjectCatalogCollector.Collect(
                [("CREATE VIEW dbo.UserEmails AS SELECT Email FROM dbo.Users;", "view.sql")], 150);
            var catalogPath = Path.Combine(directory, "objects.json");
            await File.WriteAllTextAsync(catalogPath, ObjectCatalogSerializer.Serialize(catalog));

            var result = await RunAsync([
                "schema", "diff", "--from", beforePath, "--to", afterPath, "--catalog", catalogPath
            ]);

            Assert.Equal(ExitCodes.Violations, result.Code);
            using var document = JsonDocument.Parse(result.Stdout);
            Assert.Equal(1, document.RootElement.GetProperty("summary").GetProperty("breaking").GetInt32());
            var change = document.RootElement.GetProperty("changes")[0];
            Assert.Equal("columnRemoved", change.GetProperty("kind").GetString());
            Assert.Equal("Email", change.GetProperty("columnName").GetString());
            Assert.Equal("dbo.UserEmails", change.GetProperty("impactedObjects")[0].GetProperty("id").GetString());
        }
        finally
        {
            await TempDirectoryCleanup.CleanupAsync(Environment.CurrentDirectory, directory);
        }
    }

    [Fact]
    public async Task SchemaDiff_NonBreakingAddition_WritesOutputFileAndSucceeds()
    {
        var directory = CreateDirectory();
        try
        {
            var beforePath = await WriteSnapshotAsync(directory, "before.json", includeEmail: false);
            var afterPath = await WriteSnapshotAsync(directory, "after.json", includeEmail: true);
            var outputPath = Path.Combine(directory, "diff.json");

            var result = await RunAsync([
                "schema", "diff", "--from", beforePath, "--to", afterPath, "--output", outputPath
            ]);

            Assert.Equal(0, result.Code);
            Assert.Empty(result.Stdout);
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
            Assert.Equal(0, document.RootElement.GetProperty("summary").GetProperty("breaking").GetInt32());
            Assert.Equal("columnAdded", document.RootElement.GetProperty("changes")[0].GetProperty("kind").GetString());
        }
        finally
        {
            await TempDirectoryCleanup.CleanupAsync(Environment.CurrentDirectory, directory);
        }
    }

    [Fact]
    public async Task SchemaDiff_WithoutFrom_ReturnsConfigError()
    {
        var result = await RunAsync(["schema", "diff", "--to", "after.json"]);

        Assert.Equal(ExitCodes.ConfigError, result.Code);
        Assert.Contains("--from", result.Stderr);
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static async Task<string> WriteSnapshotAsync(string directory, string fileName, bool includeEmail)
    {
        var columns = new List<ColumnSchema>
        {
            new("Id", new SqlTypeInfo("int", TypeCategory.ExactNumeric), false)
        };
        if (includeEmail)
        {
            columns.Add(new ColumnSchema(
                "Email", new SqlTypeInfo("nvarchar", TypeCategory.UnicodeString, MaxLength: 200), true));
        }
        var databases = new[] { new DatabaseSchema("AppDb", [new TableSchema("dbo", "Users", columns)], []) };
        var snapshot = new SchemaSnapshot(
            new SnapshotMetadata("2026-01-01T00:00:00Z", "localhost", "AppDb", 150,
                SchemaSnapshotSerializer.ComputeContentHash(databases)),
            databases);
        var path = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(path, SchemaSnapshotSerializer.Serialize(snapshot));
        return path;
    }

    private static async Task<CliResult> RunAsync(string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await CliApp.RunAsync(args, TextReader.Null, stdout, stderr);
        return new CliResult(code, stdout.ToString(), stderr.ToString());
    }

    private sealed record CliResult(int Code, string Stdout, string Stderr);
}
