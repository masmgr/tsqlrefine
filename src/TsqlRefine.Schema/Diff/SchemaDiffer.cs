using System.Globalization;
using TsqlRefine.Schema.Model;

namespace TsqlRefine.Schema.Diff;

/// <summary>Compares schema snapshots and classifies potentially breaking changes.</summary>
public static class SchemaDiffer
{
    public static SchemaDiff Compare(SchemaSnapshot before, SchemaSnapshot after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);

        var changes = new List<SchemaChange>();
        var beforeDatabases = IndexByName(before.Databases, static database => database.Name);
        var afterDatabases = IndexByName(after.Databases, static database => database.Name);

        AddDatabaseChanges(beforeDatabases, afterDatabases, changes);
        foreach (var databaseName in beforeDatabases.Keys.Intersect(afterDatabases.Keys, StringComparer.OrdinalIgnoreCase))
        {
            CompareDatabase(beforeDatabases[databaseName], afterDatabases[databaseName], changes);
        }

        return new SchemaDiff(changes
            .OrderBy(static change => change.DatabaseName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static change => change.SchemaName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static change => change.ObjectName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static change => change.ColumnName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static change => change.Kind)
            .ToArray());
    }

    private static void AddDatabaseChanges(
        IReadOnlyDictionary<string, DatabaseSchema> before,
        IReadOnlyDictionary<string, DatabaseSchema> after,
        List<SchemaChange> changes)
    {
        foreach (var name in before.Keys.Except(after.Keys, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new SchemaChange(SchemaChangeKind.DatabaseRemoved, true, name));
        }
        foreach (var name in after.Keys.Except(before.Keys, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new SchemaChange(SchemaChangeKind.DatabaseAdded, false, name));
        }
    }

    private static void CompareDatabase(DatabaseSchema before, DatabaseSchema after, List<SchemaChange> changes)
    {
        CompareObjects(before.Name, "table", before.Tables, after.Tables, changes);
        CompareObjects(before.Name, "view", before.Views, after.Views, changes);
    }

    private static void CompareObjects(
        string databaseName,
        string objectKind,
        IReadOnlyList<TableSchema> beforeObjects,
        IReadOnlyList<TableSchema> afterObjects,
        List<SchemaChange> changes)
    {
        var before = IndexByName(beforeObjects, ObjectKey);
        var after = IndexByName(afterObjects, ObjectKey);
        var addedKind = objectKind == "table" ? SchemaChangeKind.TableAdded : SchemaChangeKind.ViewAdded;
        var removedKind = objectKind == "table" ? SchemaChangeKind.TableRemoved : SchemaChangeKind.ViewRemoved;

        foreach (var key in before.Keys.Except(after.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var item = before[key];
            changes.Add(new SchemaChange(removedKind, true, databaseName, item.SchemaName, item.Name, objectKind));
        }
        foreach (var key in after.Keys.Except(before.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var item = after[key];
            changes.Add(new SchemaChange(addedKind, false, databaseName, item.SchemaName, item.Name, objectKind));
        }
        foreach (var key in before.Keys.Intersect(after.Keys, StringComparer.OrdinalIgnoreCase))
        {
            CompareColumns(databaseName, objectKind, before[key], after[key], changes);
        }
    }

    private static void CompareColumns(
        string databaseName,
        string objectKind,
        TableSchema beforeObject,
        TableSchema afterObject,
        List<SchemaChange> changes)
    {
        var before = IndexByName(beforeObject.Columns, static column => column.Name);
        var after = IndexByName(afterObject.Columns, static column => column.Name);

        foreach (var name in before.Keys.Except(after.Keys, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(CreateColumnChange(SchemaChangeKind.ColumnRemoved, true, beforeObject, objectKind, name));
        }
        foreach (var name in after.Keys.Except(before.Keys, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(CreateColumnChange(SchemaChangeKind.ColumnAdded, false, afterObject, objectKind, name));
        }
        foreach (var name in before.Keys.Intersect(after.Keys, StringComparer.OrdinalIgnoreCase))
        {
            var beforeColumn = before[name];
            var afterColumn = after[name];
            if (beforeColumn.Type != afterColumn.Type)
            {
                changes.Add(CreateColumnChange(
                    SchemaChangeKind.ColumnTypeChanged,
                    true,
                    beforeObject,
                    objectKind,
                    name,
                    FormatType(beforeColumn.Type),
                    FormatType(afterColumn.Type)));
            }
            if (beforeColumn.IsNullable != afterColumn.IsNullable)
            {
                changes.Add(CreateColumnChange(
                    SchemaChangeKind.ColumnNullabilityChanged,
                    !afterColumn.IsNullable,
                    beforeObject,
                    objectKind,
                    name,
                    beforeColumn.IsNullable ? "nullable" : "not null",
                    afterColumn.IsNullable ? "nullable" : "not null"));
            }
        }

        SchemaChange CreateColumnChange(
            SchemaChangeKind kind,
            bool isBreaking,
            TableSchema objectSchema,
            string kindName,
            string columnName,
            string? beforeValue = null,
            string? afterValue = null) =>
            new(kind, isBreaking, databaseName, objectSchema.SchemaName, objectSchema.Name, kindName,
                columnName, beforeValue, afterValue);
    }

    private static string FormatType(SqlTypeInfo type)
    {
        var typeName = type.TypeName.ToLowerInvariant();
        if (typeName is "decimal" or "numeric" && type.Precision is not null)
        {
            return type.Scale is null
                ? $"{type.TypeName}({type.Precision.Value.ToString(CultureInfo.InvariantCulture)})"
                : $"{type.TypeName}({type.Precision.Value.ToString(CultureInfo.InvariantCulture)},{type.Scale.Value.ToString(CultureInfo.InvariantCulture)})";
        }
        if (typeName is "datetime2" or "datetimeoffset" or "time" && type.Scale is not null)
        {
            return $"{type.TypeName}({type.Scale.Value.ToString(CultureInfo.InvariantCulture)})";
        }
        if (typeName is "char" or "varchar" or "binary" or "varbinary" or "nchar" or "nvarchar" &&
            type.MaxLength is not null)
        {
            var displayLength = typeName is "nchar" or "nvarchar" && type.MaxLength > 0
                ? type.MaxLength / 2
                : type.MaxLength;
            var length = displayLength == -1
                ? "max"
                : displayLength.Value.ToString(CultureInfo.InvariantCulture);
            return $"{type.TypeName}({length})";
        }
        return type.TypeName;
    }

    private static string ObjectKey(TableSchema item) => $"{item.SchemaName}\u001f{item.Name}";

    private static Dictionary<string, T> IndexByName<T>(IEnumerable<T> values, Func<T, string> getName) =>
        values.ToDictionary(getName, StringComparer.OrdinalIgnoreCase);
}
