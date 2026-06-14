namespace TsqlRefine.Schema.Model;

/// <summary>
/// Represents the schema of a single database, containing tables and views.
/// </summary>
/// <param name="Name">The database name.</param>
/// <param name="Tables">The tables in this database.</param>
/// <param name="Views">The views in this database (column information only).</param>
public sealed record DatabaseSchema(
    string Name,
    IReadOnlyList<TableSchema> Tables,
    IReadOnlyList<TableSchema> Views
);
