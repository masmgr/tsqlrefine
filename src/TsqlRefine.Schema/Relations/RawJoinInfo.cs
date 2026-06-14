namespace TsqlRefine.Schema.Relations;

/// <summary>
/// A single JOIN occurrence extracted from an AST, before aggregation.
/// </summary>
internal sealed record RawJoinInfo(
    string LeftSchema,
    string LeftTable,
    string RightSchema,
    string RightTable,
    string JoinType,
    IReadOnlyList<ColumnPair> ColumnPairs,
    string SourceFile,
    JoinShape ShapeFlags
);
