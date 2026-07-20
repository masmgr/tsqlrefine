using System.Collections.Frozen;
using TsqlRefine.PluginSdk;

namespace TsqlRefine.Schema.Catalog;

/// <summary>Read-only, indexed provider for a collected object catalog.</summary>
public sealed class ObjectCatalogProvider : IObjectCatalogProvider
{
    private readonly FrozenDictionary<string, CatalogObjectInfo[]> _objectsByName;
    private readonly FrozenDictionary<string, CatalogReferenceInfo[]> _referencesByTarget;
    private readonly CatalogObjectInfo[] _objects;

    /// <summary>Creates an indexed provider from a catalog model.</summary>
    public ObjectCatalogProvider(ObjectCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Scope = new CatalogScopeInfo(
            catalog.Scope.Databases,
            catalog.Scope.IsAuthoritative,
            catalog.Scope.IncludesExternalReferences,
            catalog.Scope.DefaultSchema);
        _objects = catalog.Objects.Select(ToDto).ToArray();
        _objectsByName = _objects
            .GroupBy(obj => CreateKey(obj.Id.DatabaseName, obj.Id.SchemaName, obj.Id.Name), StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        _referencesByTarget = catalog.References
            .Select(ToDto)
            .GroupBy(reference => CreateKey(
                reference.ToObject.DatabaseName,
                reference.ToObject.SchemaName,
                reference.ToObject.Name), StringComparer.OrdinalIgnoreCase)
            .ToFrozenDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool HasData => _objects.Length > 0;

    /// <inheritdoc />
    public CatalogScopeInfo Scope { get; }

    /// <inheritdoc />
    public CatalogObjectInfo? ResolveObject(
        string? database,
        string? schema,
        string name,
        CatalogObjectKindFilter kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedSchema = string.IsNullOrWhiteSpace(schema) ? Scope.DefaultSchema : schema;
        if (!_objectsByName.TryGetValue(CreateKey(database, normalizedSchema, name), out var candidates))
        {
            return null;
        }
        var matches = candidates.Where(candidate => Includes(kind, candidate.Kind)).Take(2).ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<CatalogReferenceInfo> GetReferencesTo(
        string? database,
        string schema,
        string name,
        string? column = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_referencesByTarget.TryGetValue(CreateKey(database, schema, name), out var references))
        {
            return [];
        }
        return column is null
            ? references
            : references.Where(reference =>
                string.Equals(reference.ToColumn, column, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    /// <inheritdoc />
    public IReadOnlyList<CatalogObjectInfo> GetAllObjects() => _objects;

    private static CatalogObjectInfo ToDto(CatalogObject obj) => new(
        new CatalogObjectIdInfo(obj.Id.DatabaseName, obj.Id.SchemaName, obj.Id.Name),
        obj.Kind,
        obj.Parameters.Select(parameter => new CatalogParameterInfo(
            parameter.Name,
            parameter.TypeName,
            parameter.Type,
            parameter.IsOutput,
            parameter.HasDefault)).ToArray(),
        obj.ResultColumns,
        obj.DefinedInFile,
        obj.DefinedAt);

    private static CatalogReferenceInfo ToDto(CatalogReference reference) => new(
        reference.FromObject is null
            ? null
            : new CatalogObjectIdInfo(
                reference.FromObject.DatabaseName,
                reference.FromObject.SchemaName,
                reference.FromObject.Name),
        new CatalogObjectIdInfo(
            reference.ToObject.DatabaseName,
            reference.ToObject.SchemaName,
            reference.ToObject.Name),
        reference.ToColumn,
        reference.Kind,
        reference.Resolution,
        reference.ReferencedInFile,
        reference.ReferencedAt,
        reference.IsDynamic);

    private static bool Includes(CatalogObjectKindFilter filter, CatalogObjectKind kind)
    {
        var flag = kind switch
        {
            CatalogObjectKind.Procedure => CatalogObjectKindFilter.Procedure,
            CatalogObjectKind.ScalarFunction => CatalogObjectKindFilter.ScalarFunction,
            CatalogObjectKind.TableValuedFunction => CatalogObjectKindFilter.TableValuedFunction,
            CatalogObjectKind.View => CatalogObjectKindFilter.View,
            _ => CatalogObjectKindFilter.None
        };
        return (filter & flag) != 0;
    }

    private static string CreateKey(string? database, string schema, string name) =>
        $"{database ?? string.Empty}\u001f{schema}\u001f{name}";
}
