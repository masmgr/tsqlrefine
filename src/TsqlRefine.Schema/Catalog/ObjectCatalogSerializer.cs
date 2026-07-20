using System.Text.Json;
using System.Text.Json.Serialization;

namespace TsqlRefine.Schema.Catalog;

/// <summary>Serializes and deserializes versioned object catalogs.</summary>
public static class ObjectCatalogSerializer
{
    /// <summary>The current object catalog JSON version.</summary>
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>Serializes an object catalog.</summary>
    public static string Serialize(ObjectCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return JsonSerializer.Serialize(catalog, Options);
    }

    /// <summary>Deserializes and validates an object catalog.</summary>
    public static ObjectCatalog Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Validate(JsonSerializer.Deserialize<ObjectCatalog>(json, Options));
    }

    /// <summary>Deserializes and validates an object catalog from a UTF-8 stream.</summary>
    public static ObjectCatalog Deserialize(Stream utf8Json)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);
        return Validate(JsonSerializer.Deserialize<ObjectCatalog>(utf8Json, Options));
    }

    private static ObjectCatalog Validate(ObjectCatalog? catalog)
    {
        if (catalog is null)
        {
            throw new JsonException("Failed to deserialize object catalog: result was null.");
        }
        if (catalog.Version != CurrentVersion)
        {
            throw new JsonException(
                $"Unsupported object catalog version {catalog.Version}; expected {CurrentVersion}.");
        }
        if (catalog.Scope is null || catalog.Objects is null || catalog.References is null)
        {
            throw new JsonException("Object catalog is missing required scope, objects, or references data.");
        }
        return catalog;
    }
}
