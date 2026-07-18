using TsqlRefine.PluginSdk;

namespace TsqlRefine.Schema.Catalog;

/// <summary>Identity of a collected SQL object.</summary>
public sealed record CatalogObjectId(string? DatabaseName, string SchemaName, string Name);

/// <summary>Parameter metadata stored in an object catalog.</summary>
public sealed record CatalogParameter(
    string Name,
    string TypeName,
    SchemaTypeInfo Type,
    bool IsOutput,
    bool HasDefault);

/// <summary>Definition metadata stored in an object catalog.</summary>
public sealed record CatalogObject(
    CatalogObjectId Id,
    CatalogObjectKind Kind,
    IReadOnlyList<CatalogParameter> Parameters,
    IReadOnlyList<SchemaColumnInfo>? ResultColumns,
    string DefinedInFile,
    TsqlRefine.PluginSdk.Range DefinedAt);

/// <summary>Cross-object reference stored in an object catalog.</summary>
public sealed record CatalogReference(
    CatalogObjectId? FromObject,
    CatalogObjectId ToObject,
    string? ToColumn,
    CatalogReferenceKind Kind,
    CatalogResolutionStatus Resolution,
    string ReferencedInFile,
    TsqlRefine.PluginSdk.Range ReferencedAt,
    bool IsDynamic);

/// <summary>Authority and normalization metadata for an object catalog.</summary>
public sealed record CatalogScope(
    IReadOnlyList<string> Databases,
    bool IsAuthoritative,
    bool IncludesExternalReferences,
    string DefaultSchema);

/// <summary>Versioned collection of SQL object definitions and references.</summary>
public sealed record ObjectCatalog(
    int Version,
    DateTimeOffset GeneratedAt,
    int CompatLevel,
    CatalogScope Scope,
    IReadOnlyList<CatalogObject> Objects,
    IReadOnlyList<CatalogReference> References);
