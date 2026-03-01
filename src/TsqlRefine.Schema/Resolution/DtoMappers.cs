using TsqlRefine.PluginSdk;
using TsqlRefine.Schema.Model;
using TsqlRefine.Schema.Snapshot;
using TsqlRefine.Schema.TypeSystem;

namespace TsqlRefine.Schema.Resolution;

/// <summary>
/// Maps between Schema internal models and PluginSdk DTO types.
/// </summary>
internal static class DtoMappers
{
    /// <summary>
    /// Converts a <see cref="TypeCategory"/> to a <see cref="SchemaTypeCategory"/>.
    /// </summary>
    internal static SchemaTypeCategory ToDto(this TypeCategory category) => category switch
    {
        TypeCategory.ExactNumeric => SchemaTypeCategory.ExactNumeric,
        TypeCategory.ApproximateNumeric => SchemaTypeCategory.ApproximateNumeric,
        TypeCategory.AnsiString => SchemaTypeCategory.AnsiString,
        TypeCategory.UnicodeString => SchemaTypeCategory.UnicodeString,
        TypeCategory.DateTime => SchemaTypeCategory.DateTime,
        TypeCategory.Binary => SchemaTypeCategory.Binary,
        TypeCategory.UniqueIdentifier => SchemaTypeCategory.UniqueIdentifier,
        TypeCategory.Xml => SchemaTypeCategory.Xml,
        TypeCategory.Spatial => SchemaTypeCategory.Spatial,
        _ => SchemaTypeCategory.Other,
    };

    /// <summary>
    /// Converts a <see cref="SqlTypeInfo"/> to a <see cref="SchemaTypeInfo"/>.
    /// </summary>
    internal static SchemaTypeInfo ToDto(this SqlTypeInfo typeInfo) =>
        new(typeInfo.TypeName, typeInfo.Category.ToDto(), typeInfo.MaxLength, typeInfo.Precision, typeInfo.Scale);

    /// <summary>
    /// Converts a <see cref="ColumnSchema"/> to a <see cref="SchemaColumnInfo"/>.
    /// </summary>
    internal static SchemaColumnInfo ToDto(this ColumnSchema column) =>
        new(column.Name, column.Type.ToDto(), column.IsNullable, column.IsIdentity, column.IsComputed);

    /// <summary>
    /// Converts a <see cref="SnapshotMetadata"/> to a <see cref="SchemaSnapshotMetadata"/>.
    /// </summary>
    internal static SchemaSnapshotMetadata ToDto(this SnapshotMetadata metadata) =>
        new(metadata.GeneratedAt, metadata.ServerName, metadata.DatabaseName, metadata.CompatLevel, metadata.ContentHash);

    /// <summary>
    /// Converts a <see cref="PrimaryKeyInfo"/> to a <see cref="SchemaPrimaryKeyInfo"/>.
    /// </summary>
    internal static SchemaPrimaryKeyInfo ToDto(this PrimaryKeyInfo pk) =>
        new(pk.Columns, pk.IsClustered);

    /// <summary>
    /// Converts a <see cref="UniqueConstraintInfo"/> to a <see cref="SchemaUniqueConstraintInfo"/>.
    /// </summary>
    internal static SchemaUniqueConstraintInfo ToDto(this UniqueConstraintInfo uc) =>
        new(uc.Name, uc.Columns);

    /// <summary>
    /// Converts a unique <see cref="IndexInfo"/> to a <see cref="SchemaUniqueConstraintInfo"/>.
    /// </summary>
    internal static SchemaUniqueConstraintInfo ToUniqueDto(this IndexInfo index) =>
        new(index.Name, index.Columns);

    /// <summary>
    /// Converts a <see cref="ForeignKeyInfo"/> to a <see cref="SchemaForeignKeyInfo"/>.
    /// </summary>
    internal static SchemaForeignKeyInfo ToDto(this ForeignKeyInfo fk, ResolvedTable sourceTable, ResolvedTable targetTable) =>
        new(fk.Name, sourceTable, fk.SourceColumns, targetTable, fk.TargetColumns);
}
