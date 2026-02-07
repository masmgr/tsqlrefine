using System.Text.Json;
using System.Text.Json.Serialization;

namespace TsqlRefine.Core;

/// <summary>
/// Provides default JSON serialization options for TsqlRefine.
/// </summary>
public static class JsonDefaults
{
    /// <summary>
    /// Gets the default JSON serializer options with camelCase naming, null value omission, and indented output.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };
}

