using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TsqlRefine.Schema.Model;

namespace TsqlRefine.Schema.Snapshot;

/// <summary>
/// Serializes and deserializes <see cref="SchemaSnapshot"/> to and from JSON.
/// </summary>
public static class SchemaSnapshotSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly JsonSerializerOptions DeserializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Serializes a <see cref="SchemaSnapshot"/> to a JSON string.
    /// </summary>
    /// <param name="snapshot">The snapshot to serialize.</param>
    /// <returns>The JSON representation of the snapshot.</returns>
    public static string Serialize(SchemaSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(snapshot, SerializerOptions);
    }

    /// <summary>
    /// Deserializes a <see cref="SchemaSnapshot"/> from a JSON string.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized snapshot.</returns>
    /// <exception cref="JsonException">If the JSON is invalid or cannot be deserialized.</exception>
    public static SchemaSnapshot Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<SchemaSnapshot>(json, DeserializerOptions)
            ?? throw new JsonException("Failed to deserialize schema snapshot: result was null.");
    }

    /// <summary>
    /// Deserializes a <see cref="SchemaSnapshot"/> from a UTF-8 JSON stream.
    /// </summary>
    /// <param name="utf8Json">The UTF-8 stream to deserialize.</param>
    /// <returns>The deserialized snapshot.</returns>
    /// <exception cref="JsonException">If the JSON is invalid or cannot be deserialized.</exception>
    public static SchemaSnapshot Deserialize(Stream utf8Json)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);
        return JsonSerializer.Deserialize<SchemaSnapshot>(utf8Json, DeserializerOptions)
            ?? throw new JsonException("Failed to deserialize schema snapshot: result was null.");
    }

    /// <summary>
    /// Computes a SHA-256 hash of the database content (excluding metadata) for change detection.
    /// </summary>
    /// <param name="databases">The database schemas to hash.</param>
    /// <returns>A lowercase hex-encoded SHA-256 hash string.</returns>
    public static string ComputeContentHash(IReadOnlyList<DatabaseSchema> databases)
    {
        ArgumentNullException.ThrowIfNull(databases);
        var json = JsonSerializer.Serialize(databases, SerializerOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
