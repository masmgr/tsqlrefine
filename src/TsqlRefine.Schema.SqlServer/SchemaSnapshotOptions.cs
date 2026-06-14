namespace TsqlRefine.Schema.SqlServer;

/// <summary>
/// Options for generating a schema snapshot from a database.
/// </summary>
/// <param name="IncludeSchemas">Schema names to include. When null or empty, all schemas are included.</param>
/// <param name="ExcludeSchemas">Schema names to exclude. Applied after IncludeSchemas filter.</param>
/// <param name="CompatLevel">SQL Server compatibility level to record in snapshot metadata.</param>
public sealed record SchemaSnapshotOptions(
    IReadOnlyList<string>? IncludeSchemas = null,
    IReadOnlyList<string>? ExcludeSchemas = null,
    int CompatLevel = 150
);
