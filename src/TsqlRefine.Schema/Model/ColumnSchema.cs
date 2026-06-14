namespace TsqlRefine.Schema.Model;

/// <summary>
/// Represents a column definition within a table or view.
/// </summary>
/// <param name="Name">The column name.</param>
/// <param name="Type">The column's data type information.</param>
/// <param name="IsNullable">Whether the column allows NULL values.</param>
/// <param name="IsIdentity">Whether the column is an identity column.</param>
/// <param name="IsComputed">Whether the column is a computed column.</param>
/// <param name="DefaultExpression">The default value expression, if any.</param>
/// <param name="Collation">The column's collation, if explicitly set.</param>
public sealed record ColumnSchema(
    string Name,
    SqlTypeInfo Type,
    bool IsNullable,
    bool IsIdentity = false,
    bool IsComputed = false,
    string? DefaultExpression = null,
    string? Collation = null
);
