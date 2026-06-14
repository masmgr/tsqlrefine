namespace TsqlRefine.Schema.Model;

/// <summary>
/// Represents a unique constraint on a table.
/// </summary>
/// <param name="Name">The constraint name.</param>
/// <param name="Columns">The column names that make up the unique constraint.</param>
public sealed record UniqueConstraintInfo(
    string Name,
    IReadOnlyList<string> Columns
);
