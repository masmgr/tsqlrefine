namespace TsqlRefine.Schema.Model;

/// <summary>
/// Represents a foreign key relationship between tables.
/// </summary>
/// <param name="Name">The foreign key constraint name.</param>
/// <param name="SourceColumns">The column names in the source (referencing) table.</param>
/// <param name="TargetSchema">The schema of the target (referenced) table.</param>
/// <param name="TargetTable">The name of the target (referenced) table.</param>
/// <param name="TargetColumns">The column names in the target (referenced) table.</param>
public sealed record ForeignKeyInfo(
    string Name,
    IReadOnlyList<string> SourceColumns,
    string TargetSchema,
    string TargetTable,
    IReadOnlyList<string> TargetColumns
);
