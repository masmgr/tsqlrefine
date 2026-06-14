using TsqlRefine.PluginSdk;

namespace TsqlRefine.Rules.Helpers.Schema;

/// <summary>
/// Shared equality comparers and helpers for <see cref="ResolvedTable"/> used by schema-aware rules.
/// </summary>
internal static class ResolvedTableComparers
{
    /// <summary>
    /// Compares two resolved tables by DatabaseName, SchemaName, and TableName (case-insensitive).
    /// </summary>
    public static bool TablesAreEqual(ResolvedTable a, ResolvedTable b) =>
        string.Equals(a.DatabaseName, b.DatabaseName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.SchemaName, b.SchemaName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(a.TableName, b.TableName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Case-insensitive equality comparer for (ResolvedTable, ColumnName) cache keys.
    /// </summary>
    public sealed class TableColumnKeyComparer : IEqualityComparer<(ResolvedTable Table, string ColumnName)>
    {
        public static TableColumnKeyComparer Instance { get; } = new();

        public bool Equals((ResolvedTable Table, string ColumnName) x, (ResolvedTable Table, string ColumnName) y) =>
            TablesAreEqual(x.Table, y.Table)
            && string.Equals(x.ColumnName, y.ColumnName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((ResolvedTable Table, string ColumnName) obj)
        {
            var hash = new HashCode();
            hash.Add(obj.Table.DatabaseName, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.Table.SchemaName, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.Table.TableName, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.ColumnName, StringComparer.OrdinalIgnoreCase);
            return hash.ToHashCode();
        }
    }

    /// <summary>
    /// Case-insensitive equality comparer for ResolvedTable cache keys (without IsView).
    /// </summary>
    public sealed class TableKeyComparer : IEqualityComparer<ResolvedTable>
    {
        public static TableKeyComparer Instance { get; } = new();

        public bool Equals(ResolvedTable? x, ResolvedTable? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null)
            {
                return false;
            }

            return TablesAreEqual(x, y);
        }

        public int GetHashCode(ResolvedTable obj)
        {
            var hash = new HashCode();
            hash.Add(obj.DatabaseName, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.SchemaName, StringComparer.OrdinalIgnoreCase);
            hash.Add(obj.TableName, StringComparer.OrdinalIgnoreCase);
            return hash.ToHashCode();
        }
    }
}
