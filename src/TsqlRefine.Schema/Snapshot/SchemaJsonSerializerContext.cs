using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using TsqlRefine.Schema.Model;

namespace TsqlRefine.Schema.Snapshot;

[JsonSerializable(typeof(SchemaSnapshot))]
[JsonSerializable(typeof(IReadOnlyList<DatabaseSchema>))]
[SuppressMessage(
    "Maintainability",
    "CA1506:Avoid excessive class coupling",
    Justification = "The generated JSON metadata must reference the complete snapshot object graph.")]
internal sealed partial class SchemaJsonSerializerContext : JsonSerializerContext;
