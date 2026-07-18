using TsqlRefine.Cli.Services;
using TsqlRefine.Core.Config;
using TsqlRefine.Schema.Catalog;

namespace TsqlRefine.Cli.Tests.Services;

/// <summary>
/// Tests for schema path resolution including schema.path directory shorthand.
/// </summary>
public sealed class SchemaPathResolutionTests
{
    private static CliArgs CreateArgs(
        string? schemaPath = null,
        string? relationsProfilePath = null,
        string? objectsCatalogPath = null,
        string? configPath = null)
    {
        return CliParser.Parse(
            schemaPath is not null
                ? ["lint", "--stdin", "--schema", schemaPath]
                : relationsProfilePath is not null
                    ? ["lint", "--stdin", "--relations-profile", relationsProfilePath]
                    : objectsCatalogPath is not null
                        ? ["lint", "--stdin", "--objects-catalog", objectsCatalogPath]
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

    [Fact]
    public void ResolveObjectCatalogPath_WithSchemaPath_DerivesObjectsJson()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var args = CreateArgs();
        var config = new TsqlRefineConfig(Schema: new SchemaConfig(Path: tempDir));

        var (path, source) = ConfigLoader.ResolveObjectCatalogPath(args, config);

        Assert.Equal(Path.Combine(tempDir, "objects.json"), path);
        Assert.Contains("schema.path", source);
    }

    [Fact]
    public void ResolveObjectCatalogPath_CliOverridesConfig()
    {
        var cliPath = Path.Combine(Path.GetTempPath(), "cli-objects.json");
        var args = CreateArgs(objectsCatalogPath: cliPath);
        var config = new TsqlRefineConfig(
            Schema: new SchemaConfig(ObjectsCatalogPath: "config-objects.json"));

        var (path, source) = ConfigLoader.ResolveObjectCatalogPath(args, config);

        Assert.Equal(Path.GetFullPath(cliPath), path);
        Assert.Equal("--objects-catalog", source);
    }

    [Fact]
    public void LoadObjectCatalog_WithSchemaPathAndNoSnapshot_LoadsCatalogIndependently()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var catalog = ObjectCatalogCollector.Collect(
                [("CREATE PROCEDURE dbo.Ping AS SELECT 1;", "ping.sql")],
                150);
            File.WriteAllText(
                Path.Combine(tempDir, "objects.json"),
                ObjectCatalogSerializer.Serialize(catalog));
            var args = CreateArgs();
            var config = new TsqlRefineConfig(Schema: new SchemaConfig(Path: tempDir));

            var schemaContext = ConfigLoader.LoadSchemaContext(args, config);
            var objectCatalog = ConfigLoader.LoadObjectCatalog(args, config);

            Assert.Null(schemaContext);
            Assert.NotNull(objectCatalog);
            Assert.True(objectCatalog.HasData);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
