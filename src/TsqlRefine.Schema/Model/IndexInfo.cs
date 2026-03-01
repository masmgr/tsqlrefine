namespace TsqlRefine.Schema.Model;

/// <summary>
/// Represents an index on a table.
/// </summary>
/// <param name="Name">The index name.</param>
/// <param name="Columns">The column names included in the index key.</param>
/// <param name="IsUnique">Whether the index enforces uniqueness.</param>
/// <param name="IsClustered">Whether the index is clustered.</param>
public sealed record IndexInfo(
    string Name,
    IReadOnlyList<string> Columns,
    bool IsUnique,
    bool IsClustered
);
