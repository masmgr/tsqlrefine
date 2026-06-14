using TsqlRefine.Cli.Services;
using TsqlRefine.Core.Config;

namespace TsqlRefine.Cli.Tests.Services;

/// <summary>
/// Tests for schema path resolution including schema.path directory shorthand.
/// </summary>
public sealed class SchemaPathResolutionTests
{
    private static CliArgs CreateArgs(string? schemaPath = null, string? relationsProfilePath = null, string? configPath = null)
    {
        return CliParser.Parse(
            schemaPath is not null
                ? ["lint", "--stdin", "--schema", schemaPath]
                : relationsProfilePath is not null
                    ? ["lint", "--stdin", "--relations-profile", relationsProfilePath]
                    : configPath is not null
                        ? ["lint", "--stdin", "--config", configPath]
                        : ["lint", "--stdin"]);
    }

    [Fact]
    public void ResolveSchemaPath_WithCliSchema_ReturnsCLIPath()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var args = CreateArgs(schemaPath: tempFile);
            var config = new TsqlRefineConfig();

            var (path, source) = ConfigLoader.ResolveSchemaPath(args, config);

            Assert.Equal(Path.GetFullPath(tempFile), path);
            Assert.Equal("--schema", source);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ResolveSchemaPath_WithSchemaPath_DerivesSchemaJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var args = CreateArgs();
            var schemaConfig = new SchemaConfig(Path: tempDir);
            var config = new TsqlRefineConfig(Schema: schemaConfig);

            var (path, source) = ConfigLoader.ResolveSchemaPath(args, config);

            Assert.NotNull(path);
            Assert.Equal(Path.Combine(tempDir, "schema.json"), path);
            Assert.Contains("schema.path", source);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveSchemaPath_WithSnapshotPathOverridesSchemaPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var args = CreateArgs();
            var snapshotPath = Path.Combine(tempDir, "custom-snapshot.json");
            var schemaConfig = new SchemaConfig(Path: tempDir, SnapshotPath: snapshotPath);
            var config = new TsqlRefineConfig(Schema: schemaConfig);

            var (path, source) = ConfigLoader.ResolveSchemaPath(args, config);

            Assert.NotNull(path);
            Assert.Equal(Path.GetFullPath(snapshotPath), path);
            Assert.Equal("config", source);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveRelationsProfilePath_WithSchemaPath_DerivesRelationsJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var args = CreateArgs();
            var schemaConfig = new SchemaConfig(Path: tempDir);
            var config = new TsqlRefineConfig(Schema: schemaConfig);

            var (path, source) = ConfigLoader.ResolveRelationsProfilePath(args, config);

            Assert.NotNull(path);
            Assert.Equal(Path.Combine(tempDir, "relations.json"), path);
            Assert.Contains("schema.path", source);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveRelationsProfilePath_WithRelationsProfilePathOverridesSchemaPath()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var args = CreateArgs();
            var relationsPath = Path.Combine(tempDir, "custom-relations.json");
            var schemaConfig = new SchemaConfig(Path: tempDir, RelationsProfilePath: relationsPath);
            var config = new TsqlRefineConfig(Schema: schemaConfig);

            var (path, source) = ConfigLoader.ResolveRelationsProfilePath(args, config);

            Assert.NotNull(path);
            Assert.Equal(Path.GetFullPath(relationsPath), path);
            Assert.Equal("config", source);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ResolveSchemaPath_NoSchemaConfigured_ReturnsNull()
    {
        var args = CreateArgs();
        var config = new TsqlRefineConfig();

        var (path, source) = ConfigLoader.ResolveSchemaPath(args, config);

        Assert.Null(path);
        Assert.Equal("none", source);
    }
}
