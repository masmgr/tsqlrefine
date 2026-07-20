using System.Reflection;
using System.Text.Json;
using TsqlRefine.Schema.Catalog;
using TsqlRefine.Schema.Diff;
using TsqlRefine.Schema.Model;
using TsqlRefine.Schema.Snapshot;

namespace TsqlRefine.Cli.Services;

public sealed record SchemaDiffSummary(int Total, int Breaking, int NonBreaking);

public sealed record SchemaDiffOutputChange(
    string Kind,
    bool IsBreaking,
    string DatabaseName,
    string? SchemaName,
    string? ObjectName,
    string? ObjectKind,
    string? ColumnName,
    string? Before,
    string? After,
    IReadOnlyList<ImpactedCatalogObject> ImpactedObjects);

public sealed record SchemaDiffResult(
    int SchemaVersion,
    string Tool,
    string Version,
    string BeforeHash,
    string AfterHash,
    SchemaDiffSummary Summary,
    IReadOnlyList<SchemaDiffOutputChange> Changes);

public static class SchemaDiffService
{
    public const int CurrentSchemaVersion = 1;

    public static SchemaSnapshot LoadSnapshot(string? path, string optionName)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ConfigException($"schema diff requires {optionName} <schema.json>.");
        }
        if (!File.Exists(path))
        {
            throw new ConfigException($"Schema snapshot not found: {path}");
        }
        try
        {
            using var stream = File.OpenRead(path);
            return SchemaSnapshotSerializer.Deserialize(stream);
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"Failed to parse schema snapshot '{path}': {ex.Message}");
        }
        catch (IOException ex)
        {
            throw new ConfigException($"Failed to read schema snapshot '{path}': {ex.Message}");
        }
    }

    public static ObjectCatalog? LoadOptionalCatalog(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        return CatalogAnalysisService.LoadCatalog(path);
    }

    public static SchemaDiffResult CreateResult(
        SchemaSnapshot before,
        SchemaSnapshot after,
        ObjectCatalog? catalog)
    {
        var diff = SchemaDiffer.Compare(before, after);
        var changes = diff.Changes.Select(change => new SchemaDiffOutputChange(
            ToCamelCase(change.Kind.ToString()),
            change.IsBreaking,
            change.DatabaseName,
            change.SchemaName,
            change.ObjectName,
            change.ObjectKind,
            change.ColumnName,
            change.Before,
            change.After,
            FindImpacts(change, catalog))).ToArray();
        return new SchemaDiffResult(
            CurrentSchemaVersion,
            "tsqlrefine",
            GetVersion(),
            before.Metadata.ContentHash,
            after.Metadata.ContentHash,
            new SchemaDiffSummary(changes.Length, diff.BreakingChangeCount, changes.Length - diff.BreakingChangeCount),
            changes);
    }

    private static IReadOnlyList<ImpactedCatalogObject> FindImpacts(SchemaChange change, ObjectCatalog? catalog)
    {
        if (catalog is null || !change.IsBreaking || change.SchemaName is null || change.ObjectName is null)
        {
            return [];
        }

        var hasQualifiedDatabase = catalog.References.Any(reference =>
            string.Equals(reference.ToObject.DatabaseName, change.DatabaseName, StringComparison.OrdinalIgnoreCase));
        var table = hasQualifiedDatabase
            ? $"{change.DatabaseName}.{change.SchemaName}.{change.ObjectName}"
            : $"{change.SchemaName}.{change.ObjectName}";
        return CatalogAnalysisService.AnalyzeImpact(catalog, table, change.ColumnName).ImpactedObjects;
    }

    private static string ToCamelCase(string value) =>
        char.ToLowerInvariant(value[0]) + value[1..];

    private static string GetVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
