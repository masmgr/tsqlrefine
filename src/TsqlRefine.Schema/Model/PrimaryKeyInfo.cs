namespace TsqlRefine.Schema.Model;

/// <summary>
/// Represents the primary key constraint of a table.
/// </summary>
/// <param name="Columns">The column names that make up the primary key.</param>
/// <param name="IsClustered">Whether the primary key is clustered.</param>
public sealed record PrimaryKeyInfo(
    IReadOnlyList<string> Columns,
    bool IsClustered
);
