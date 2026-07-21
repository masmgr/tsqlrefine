using System.Security.Cryptography;
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

    private static readonly SchemaJsonSerializerContext SerializerContext = new(SerializerOptions);
    private static readonly SchemaJsonSerializerContext DeserializerContext = new(DeserializerOptions);

    /// <summary>
    /// Serializes a <see cref="SchemaSnapshot"/> to a JSON string.
    /// </summary>
    /// <param name="snapshot">The snapshot to serialize.</param>
    /// <returns>The JSON representation of the snapshot.</returns>
    public static string Serialize(SchemaSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(snapshot, SerializerContext.SchemaSnapshot);
    }

    /// <summary>
    /// Asynchronously serializes a <see cref="SchemaSnapshot"/> to a UTF-8 JSON stream.
    /// </summary>
    /// <param name="utf8Json">The destination UTF-8 stream.</param>
    /// <param name="snapshot">The snapshot to serialize.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static Task SerializeAsync(
        Stream utf8Json,
        SchemaSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.SerializeAsync(
            utf8Json,
            snapshot,
            SerializerContext.SchemaSnapshot,
            cancellationToken);
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
        return JsonSerializer.Deserialize(json, DeserializerContext.SchemaSnapshot)
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
        return JsonSerializer.Deserialize(utf8Json, DeserializerContext.SchemaSnapshot)
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
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var stream = new IncrementalHashStream(hash);
        JsonSerializer.Serialize(
            stream,
            databases,
            SerializerContext.IReadOnlyListDatabaseSchema);
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private sealed class IncrementalHashStream(IncrementalHash hash) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override void Write(byte[] buffer, int offset, int count) =>
            hash.AppendData(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer) => hash.AppendData(buffer);

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
