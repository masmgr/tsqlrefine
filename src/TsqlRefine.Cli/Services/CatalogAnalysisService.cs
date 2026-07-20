using System.Reflection;
using System.Text;
using System.Text.Json;
using TsqlRefine.Core;
using TsqlRefine.Schema.Catalog;

namespace TsqlRefine.Cli.Services;

public sealed record CatalogAnalysisTarget(
    string? DatabaseName,
    string SchemaName,
    string Name,
    string? Column);

public sealed record ImpactedCatalogObject(
    string Id,
    string Kind,
    string DefinedInFile,
    TsqlRefine.PluginSdk.Range DefinedAt,
    int Depth);

public sealed record ImpactAnalysisResult(
    int SchemaVersion,
    string Tool,
    string Version,
    CatalogAnalysisTarget Target,
    IReadOnlyList<ImpactedCatalogObject> ImpactedObjects);

public sealed record DependencyGraphNode(
    string Id,
    string Kind,
    string DefinedInFile,
    TsqlRefine.PluginSdk.Range DefinedAt);

public sealed record DependencyGraphEdge(
    string From,
    string To,
    string Kind,
    string Resolution,
    string? Column,
    string ReferencedInFile,
    TsqlRefine.PluginSdk.Range ReferencedAt);

public sealed record DependencyGraphResult(
    int SchemaVersion,
    string Tool,
    string Version,
    IReadOnlyList<DependencyGraphNode> Nodes,
    IReadOnlyList<DependencyGraphEdge> Edges);

[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506", Justification = "Existing catalog analysis service; tracked as coupling baseline debt.")]
public static class CatalogAnalysisService
{
    public const int CurrentSchemaVersion = 1;

    public static ObjectCatalog LoadCatalog(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ConfigException("The analyze command requires --catalog <objects.json>.");
        }
        if (!File.Exists(path))
        {
            throw new ConfigException($"Object catalog not found: {path}");
        }
        try
        {
            using var stream = File.OpenRead(path);
            return ObjectCatalogSerializer.Deserialize(stream);
        }
        catch (JsonException ex)
        {
            throw new ConfigException($"Failed to parse object catalog: {ex.Message}");
        }
        catch (IOException ex)
        {
            throw new ConfigException($"Failed to read object catalog: {ex.Message}");
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1502", Justification = "Existing impact analysis decision tree; tracked as complexity baseline debt.")]
    public static ImpactAnalysisResult AnalyzeImpact(ObjectCatalog catalog, string? table, string? column)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var target = ParseTarget(table, column, catalog.Scope.DefaultSchema);
        var objectsById = catalog.Objects.ToDictionary(obj => CreateKey(obj.Id), StringComparer.OrdinalIgnoreCase);
        var reverseDependencies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in catalog.References.Where(reference => reference.FromObject is not null && !reference.IsDynamic))
        {
            var targetKey = CreateKey(reference.ToObject);
            var sourceKey = CreateKey(reference.FromObject!);
            if (!objectsById.ContainsKey(sourceKey))
            {
                continue;
            }
            if (!reverseDependencies.TryGetValue(targetKey, out var sources))
            {
                sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                reverseDependencies[targetKey] = sources;
            }
            sources.Add(sourceKey);
        }

        var depths = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        foreach (var reference in catalog.References.Where(reference =>
                     reference.FromObject is not null &&
                     !reference.IsDynamic &&
                     TargetEquals(reference.ToObject, target) &&
                     (target.Column is null || string.Equals(reference.ToColumn, target.Column, StringComparison.OrdinalIgnoreCase))))
        {
            var sourceKey = CreateKey(reference.FromObject!);
            if (objectsById.ContainsKey(sourceKey) && depths.TryAdd(sourceKey, 1))
            {
                queue.Enqueue(sourceKey);
            }
        }

        while (queue.TryDequeue(out var affectedKey))
        {
            if (!reverseDependencies.TryGetValue(affectedKey, out var callers))
            {
                continue;
            }
            foreach (var caller in callers)
            {
                var depth = depths[affectedKey] + 1;
                if (!depths.TryGetValue(caller, out var currentDepth) || depth < currentDepth)
                {
                    depths[caller] = depth;
                    queue.Enqueue(caller);
                }
            }
        }

        var impacted = depths
            .Select(pair => (Object: objectsById[pair.Key], Depth: pair.Value))
            .OrderBy(item => item.Depth)
            .ThenBy(item => DisplayName(item.Object.Id), StringComparer.OrdinalIgnoreCase)
            .Select(item => new ImpactedCatalogObject(
                DisplayName(item.Object.Id),
                item.Object.Kind.ToString(),
                item.Object.DefinedInFile,
                item.Object.DefinedAt,
                item.Depth))
            .ToArray();
        return new ImpactAnalysisResult(
            CurrentSchemaVersion,
            "tsqlrefine",
            GetVersion(),
            target,
            impacted);
    }

    public static DependencyGraphResult CreateGraph(ObjectCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var nodes = catalog.Objects
            .OrderBy(obj => DisplayName(obj.Id), StringComparer.OrdinalIgnoreCase)
            .Select(obj => new DependencyGraphNode(
                DisplayName(obj.Id),
                obj.Kind.ToString(),
                obj.DefinedInFile,
                obj.DefinedAt))
            .ToArray();
        var edges = catalog.References
            .Where(reference => reference.FromObject is not null && !reference.IsDynamic)
            .OrderBy(reference => DisplayName(reference.FromObject!), StringComparer.OrdinalIgnoreCase)
            .ThenBy(reference => DisplayName(reference.ToObject), StringComparer.OrdinalIgnoreCase)
            .Select(reference => new DependencyGraphEdge(
                DisplayName(reference.FromObject!),
                DisplayName(reference.ToObject),
                reference.Kind.ToString(),
                reference.Resolution.ToString(),
                reference.ToColumn,
                reference.ReferencedInFile,
                reference.ReferencedAt))
            .ToArray();
        return new DependencyGraphResult(
            CurrentSchemaVersion,
            "tsqlrefine",
            GetVersion(),
            nodes,
            edges);
    }

    public static async Task WriteJsonAsync<T>(T value, string? outputPath, TextWriter stdout)
    {
        var content = JsonSerializer.Serialize(value, JsonDefaults.Options) + Environment.NewLine;
        await WriteAsync(content, outputPath, stdout);
    }

    public static async Task WriteDotAsync(
        DependencyGraphResult graph,
        string? outputPath,
        TextWriter stdout)
    {
        var knownNodes = graph.Nodes.Select(node => node.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var externalNodes = graph.Edges.Select(edge => edge.To)
            .Where(target => !knownNodes.Contains(target))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase);
        var builder = new StringBuilder("digraph tsqlrefine {\n  rankdir=LR;\n");
        foreach (var node in graph.Nodes)
        {
            builder.Append("  \"").Append(EscapeDot(node.Id)).Append("\" [label=\"")
                .Append(EscapeDot(node.Id)).Append("\\n").Append(EscapeDot(node.Kind)).Append("\"];\n");
        }
        foreach (var node in externalNodes)
        {
            builder.Append("  \"").Append(EscapeDot(node)).Append("\" [style=dashed];\n");
        }
        foreach (var edge in graph.Edges)
        {
            builder.Append("  \"").Append(EscapeDot(edge.From)).Append("\" -> \"")
                .Append(EscapeDot(edge.To)).Append("\" [label=\"").Append(EscapeDot(edge.Kind));
            if (edge.Column is not null)
            {
                builder.Append(':').Append(EscapeDot(edge.Column));
            }
            builder.Append("\"];\n");
        }
        builder.Append("}\n");
        await WriteAsync(builder.ToString(), outputPath, stdout);
    }

    private static async Task WriteAsync(string content, string? outputPath, TextWriter stdout)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            await stdout.WriteAsync(content);
            return;
        }
        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content, new UTF8Encoding(false));
    }

    private static CatalogAnalysisTarget ParseTarget(string? table, string? column, string defaultSchema)
    {
        if (string.IsNullOrWhiteSpace(table))
        {
            throw new ConfigException("analyze impact requires --table <table>.");
        }
        var parts = table.Split('.', StringSplitOptions.TrimEntries)
            .Select(UnquoteIdentifier)
            .ToArray();
        return parts.Length switch
        {
            1 when parts[0].Length > 0 => new CatalogAnalysisTarget(null, defaultSchema, parts[0], column),
            2 when parts.All(part => part.Length > 0) => new CatalogAnalysisTarget(null, parts[0], parts[1], column),
            3 when parts.All(part => part.Length > 0) => new CatalogAnalysisTarget(parts[0], parts[1], parts[2], column),
            _ => throw new ConfigException(
                $"Invalid --table value: '{table}'. Expected a one-, two-, or three-part SQL name.")
        };
    }

    private static string UnquoteIdentifier(string value) =>
        value.Length >= 2 && value[0] == '[' && value[^1] == ']'
            ? value[1..^1].Replace("]]", "]", StringComparison.Ordinal)
            : value;

    private static bool TargetEquals(CatalogObjectId id, CatalogAnalysisTarget target) =>
        string.Equals(id.DatabaseName ?? string.Empty, target.DatabaseName ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(id.SchemaName, target.SchemaName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(id.Name, target.Name, StringComparison.OrdinalIgnoreCase);

    private static string CreateKey(CatalogObjectId id) =>
        $"{id.DatabaseName ?? string.Empty}\u001f{id.SchemaName}\u001f{id.Name}";

    private static string DisplayName(CatalogObjectId id) =>
        string.IsNullOrWhiteSpace(id.DatabaseName)
            ? $"{id.SchemaName}.{id.Name}"
            : $"{id.DatabaseName}.{id.SchemaName}.{id.Name}";

    private static string EscapeDot(string value) => value
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("\"", "\\\"", StringComparison.Ordinal)
        .Replace("\r", string.Empty, StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string GetVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }
}
