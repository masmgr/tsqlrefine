using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TsqlRefine.Schema.Relations;

/// <summary>
/// Serializes and deserializes <see cref="RelationProfile"/> to and from JSON.
/// </summary>
public static class RelationProfileSerializer
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
    /// Serializes a <see cref="RelationProfile"/> to a JSON string.
    /// </summary>
    public static string Serialize(RelationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Serialize(profile, SerializerOptions);
    }

    /// <summary>
    /// Deserializes a <see cref="RelationProfile"/> from a JSON string.
    /// </summary>
    public static RelationProfile Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<RelationProfile>(json, DeserializerOptions)
            ?? throw new JsonException("Failed to deserialize relation profile: result was null.");
    }

    /// <summary>
    /// Deserializes a <see cref="RelationProfile"/> from a UTF-8 JSON stream.
    /// </summary>
    public static RelationProfile Deserialize(Stream utf8Json)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);
        return JsonSerializer.Deserialize<RelationProfile>(utf8Json, DeserializerOptions)
            ?? throw new JsonException("Failed to deserialize relation profile: result was null.");
    }

    /// <summary>
    /// Computes a SHA-256 hash of the relation content (excluding metadata) for change detection.
    /// </summary>
    public static string ComputeContentHash(IReadOnlyList<TableRelation> relations)
    {
        ArgumentNullException.ThrowIfNull(relations);
        var json = JsonSerializer.Serialize(relations, SerializerOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
