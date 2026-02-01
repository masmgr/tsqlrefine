using System.Collections.Frozen;

namespace TsqlRefine.Formatting.Helpers;

/// <summary>
/// Registry of system schemas for system table detection.
/// </summary>
internal static class SystemSchemaRegistry
{
    /// <summary>
    /// System schemas that contain system objects (sys.*, information_schema.*)
    /// Note: dbo is NOT included as it's the default user schema, not a system schema.
    /// </summary>
    public static readonly FrozenSet<string> SystemSchemas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "sys",
        "information_schema"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Checks if the given schema name is a system schema.
    /// </summary>
    public static bool IsSystemSchema(string? schemaName) =>
        !string.IsNullOrEmpty(schemaName) && SystemSchemas.Contains(schemaName);
}
